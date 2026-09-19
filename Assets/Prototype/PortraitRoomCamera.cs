using UnityEngine;

// Fixed-zoom gameplay framing for one bounded portrait room. This is deliberately a
// small scene component, not a reusable cinematic-camera framework.
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class PortraitRoomCamera : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Camera arenaCamera;
    [SerializeField] Transform target;
    [SerializeField] RoomPlayableBounds roomBounds;

    [Header("Portrait Camera")]
    [SerializeField, Min(.1f)] float orthographicSize = 6.25f;
    [SerializeField, Min(.001f)] float followSmoothTime = .12f;
    [SerializeField, Min(0f)] float horizontalDeadZone = .60f;
    [SerializeField, Min(0f)] float verticalDeadZone = .95f;
    [SerializeField] float verticalCompositionOffset = 1.25f;
    [SerializeField, Min(0f)] float boundsPadding = .15f;

    [Header("Wall Reveal")]
    [SerializeField, Min(0f)] float horizontalWallReveal = .6f;
    [SerializeField, Min(0f)] float verticalWallReveal = .35f;
    [SerializeField, Min(0f)] float northVisualReveal = .75f;

    Vector3 focusToCamera;
    Vector2 followVelocity;
    int viewportWidth;
    int viewportHeight;
    bool hasFocusOffset;
    bool hasSuppliedViewport;

    public Camera ArenaCamera => arenaCamera;
    public float OrthographicSize => orthographicSize;
    public RoomPlayableBounds RoomBounds => roomBounds;
    public float HorizontalWallReveal => horizontalWallReveal;
    public float VerticalWallReveal => verticalWallReveal;
    public float NorthVisualReveal => northVisualReveal;

    void Reset() => arenaCamera = GetComponent<Camera>();

    void Awake()
    {
        if (arenaCamera == null) arenaCamera = GetComponent<Camera>();
        RefreshViewport();
        CacheFocusOffset();
    }

    void LateUpdate()
    {
        RefreshViewportIfNeeded();
        TickForValidation(Time.deltaTime);
    }

    void OnValidate()
    {
        orthographicSize = Mathf.Max(.1f, orthographicSize);
        followSmoothTime = Mathf.Max(.001f, followSmoothTime);
        horizontalDeadZone = Mathf.Max(0f, horizontalDeadZone);
        verticalDeadZone = Mathf.Max(0f, verticalDeadZone);
        boundsPadding = Mathf.Max(0f, boundsPadding);
        horizontalWallReveal = Mathf.Max(0f, horizontalWallReveal);
        verticalWallReveal = Mathf.Max(0f, verticalWallReveal);
        northVisualReveal = Mathf.Max(0f, northVisualReveal);
        if (arenaCamera == null) arenaCamera = GetComponent<Camera>();
    }

    public void Configure(Transform followTarget, RoomPlayableBounds authoredBounds)
    {
        target = followTarget;
        roomBounds = authoredBounds;
        if (arenaCamera == null) arenaCamera = GetComponent<Camera>();
        if (arenaCamera != null)
        {
            arenaCamera.orthographic = true;
            arenaCamera.orthographicSize = orthographicSize;
        }
        hasFocusOffset = false;
    }

    // The reveal frame affects only this camera's clamp calculations. It never changes the
    // authored playable volume consumed by player containment, enemies, spawns, or exits.
    public void SetWallRevealMargins(float horizontal, float vertical)
    {
        horizontalWallReveal = Mathf.Max(0f, horizontal);
        verticalWallReveal = Mathf.Max(0f, vertical);
    }

    public void RefreshViewport()
    {
        hasSuppliedViewport = false;
        ApplyViewport(Screen.width, Screen.height);
    }

    // safeArea is intentionally not used to shrink the gameplay render surface. It remains
    // part of this API so test captures make that portrait/full-bleed contract explicit.
    public void RefreshViewport(int screenWidth, int screenHeight, Rect safeArea)
    {
        hasSuppliedViewport = true;
        ApplyViewport(screenWidth, screenHeight);
    }

    void ApplyViewport(int screenWidth, int screenHeight)
    {
        if (arenaCamera == null) arenaCamera = GetComponent<Camera>();
        if (arenaCamera == null || screenWidth <= 0 || screenHeight <= 0) return;

        viewportWidth = screenWidth;
        viewportHeight = screenHeight;
        arenaCamera.rect = new Rect(0f, 0f, 1f, 1f);
        arenaCamera.aspect = screenWidth / (float)screenHeight;
        arenaCamera.orthographic = true;
        // Aspect changes alter only the footprint and clamp range, never this zoom value.
        arenaCamera.orthographicSize = orthographicSize;
    }

    public void SnapToTarget()
    {
        RefreshViewportIfNeeded();
        if (!TryGetDesiredFocus(out Vector3 desiredFocus)) return;

        CacheFocusOffset();
        desiredFocus = ClampFocus(desiredFocus);
        SetFocus(desiredFocus);
        followVelocity = Vector2.zero;
    }

    // Deterministic entry point used by the existing editor smoke harness.
    public void TickForValidation(float deltaTime)
    {
        if (!TryGetDesiredFocus(out Vector3 targetFocus)) return;
        if (!hasFocusOffset) CacheFocusOffset();
        if (!hasFocusOffset) return;

        if (!TryGetGroundPoint(new Vector2(.5f, .5f), out Vector3 currentFocus)) return;

        Vector3 requestedFocus = currentFocus;
        float deltaX = targetFocus.x - currentFocus.x;
        float deltaZ = targetFocus.z - currentFocus.z;
        if (Mathf.Abs(deltaX) > horizontalDeadZone)
            requestedFocus.x += deltaX - Mathf.Sign(deltaX) * horizontalDeadZone;
        if (Mathf.Abs(deltaZ) > verticalDeadZone)
            requestedFocus.z += deltaZ - Mathf.Sign(deltaZ) * verticalDeadZone;

        requestedFocus = ClampFocus(requestedFocus);
        float smoothTime = Mathf.Max(.001f, followSmoothTime);
        float nextX = Mathf.SmoothDamp(currentFocus.x, requestedFocus.x, ref followVelocity.x,
            smoothTime, Mathf.Infinity, Mathf.Max(0f, deltaTime));
        float nextZ = Mathf.SmoothDamp(currentFocus.z, requestedFocus.z, ref followVelocity.y,
            smoothTime, Mathf.Infinity, Mathf.Max(0f, deltaTime));
        Vector3 nextFocus = ClampFocus(new Vector3(nextX, currentFocus.y, nextZ));
        if (!Mathf.Approximately(nextFocus.x, nextX)) followVelocity.x = 0f;
        if (!Mathf.Approximately(nextFocus.z, nextZ)) followVelocity.y = 0f;
        SetFocus(nextFocus);
    }

    public bool TryGetGroundFootprint(out Bounds footprint)
    {
        footprint = default;
        bool initialized = false;
        foreach (Vector2 corner in new[]
                 {
                     new Vector2(0f, 0f), new Vector2(0f, 1f),
                     new Vector2(1f, 0f), new Vector2(1f, 1f)
                 })
        {
            if (!TryGetGroundPoint(corner, out Vector3 point)) return false;
            if (!initialized)
            {
                footprint = new Bounds(point, Vector3.zero);
                initialized = true;
            }
            else footprint.Encapsulate(point);
        }
        return initialized;
    }

    public bool TryGetCameraClampBounds(out Bounds clampBounds)
    {
        clampBounds = default;
        if (roomBounds == null || !roomBounds.TryGetWorldBounds(out Bounds playableBounds)) return false;

        clampBounds = playableBounds;
        clampBounds.Expand(new Vector3(horizontalWallReveal * 2f, 0f, verticalWallReveal * 2f));
        Vector3 maximum = clampBounds.max;
        maximum.z += northVisualReveal;
        clampBounds.max = maximum;
        return true;
    }

    void RefreshViewportIfNeeded()
    {
        // A supplied viewport belongs to an explicit capture or deterministic test and must
        // not be replaced by the host editor's (often landscape) Game-view dimensions.
        if (hasSuppliedViewport) return;
        if (Screen.width != viewportWidth || Screen.height != viewportHeight)
            RefreshViewport();
    }

    bool TryGetDesiredFocus(out Vector3 desiredFocus)
    {
        desiredFocus = default;
        if (target == null || roomBounds == null || !roomBounds.TryGetWorldBounds(out Bounds bounds)) return false;

        desiredFocus = target.position;
        desiredFocus.y = bounds.center.y;
        desiredFocus.z += verticalCompositionOffset;
        return true;
    }

    void CacheFocusOffset()
    {
        if (!TryGetGroundPoint(new Vector2(.5f, .5f), out Vector3 focus))
        {
            hasFocusOffset = false;
            return;
        }

        focusToCamera = transform.position - focus;
        hasFocusOffset = true;
    }

    void SetFocus(Vector3 focus)
    {
        transform.position = focus + focusToCamera;
    }

    Vector3 ClampFocus(Vector3 focus)
    {
        if (!TryGetCameraClampBounds(out Bounds bounds) ||
            !TryGetGroundOffsets(out Vector2 minimumOffset, out Vector2 maximumOffset)) return focus;

        float minimumX = bounds.min.x + boundsPadding - minimumOffset.x;
        float maximumX = bounds.max.x - boundsPadding - maximumOffset.x;
        float minimumZ = bounds.min.z + boundsPadding - minimumOffset.y;
        float maximumZ = bounds.max.z - boundsPadding - maximumOffset.y;

        focus.x = ClampOrCenter(focus.x, minimumX, maximumX);
        focus.z = ClampOrCenter(focus.z, minimumZ, maximumZ);
        focus.y = bounds.center.y;
        return focus;
    }

    bool TryGetGroundOffsets(out Vector2 minimumOffset, out Vector2 maximumOffset)
    {
        minimumOffset = maximumOffset = default;
        if (!TryGetGroundPoint(new Vector2(.5f, .5f), out Vector3 center)) return false;

        bool initialized = false;
        foreach (Vector2 corner in new[]
                 {
                     new Vector2(0f, 0f), new Vector2(0f, 1f),
                     new Vector2(1f, 0f), new Vector2(1f, 1f)
                 })
        {
            if (!TryGetGroundPoint(corner, out Vector3 point)) return false;
            Vector2 offset = new(point.x - center.x, point.z - center.z);
            if (!initialized)
            {
                minimumOffset = maximumOffset = offset;
                initialized = true;
            }
            else
            {
                minimumOffset = Vector2.Min(minimumOffset, offset);
                maximumOffset = Vector2.Max(maximumOffset, offset);
            }
        }
        return initialized;
    }

    bool TryGetGroundPoint(Vector2 viewportPoint, out Vector3 groundPoint)
    {
        groundPoint = default;
        if (arenaCamera == null || roomBounds == null || !roomBounds.TryGetWorldBounds(out Bounds bounds)) return false;

        Ray ray = arenaCamera.ViewportPointToRay(new Vector3(viewportPoint.x, viewportPoint.y, 0f));
        if (Mathf.Abs(ray.direction.y) < .0001f) return false;
        float distance = (bounds.center.y - ray.origin.y) / ray.direction.y;
        if (distance < 0f) return false;
        groundPoint = ray.GetPoint(distance);
        return true;
    }

    static float ClampOrCenter(float value, float minimum, float maximum)
    {
        return minimum > maximum ? (minimum + maximum) * .5f : Mathf.Clamp(value, minimum, maximum);
    }
}
