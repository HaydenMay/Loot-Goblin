using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// Owns one forgiving, lower-left touch gesture and presents it as a movement vector.
/// The gameplay owner decides how that vector affects the player.
/// </summary>
[DisallowMultipleComponent]
public sealed class FloatingJoystick : MonoBehaviour
{
    const float DefaultZoneWidth = .45f;
    const float DefaultZoneTop = .62f;
    [SerializeField, Range(.3f, .5f)] float zoneWidth = DefaultZoneWidth;
    [SerializeField, Range(.5f, .8f)] float zoneTop = DefaultZoneTop;
    [SerializeField, Min(48f)] float radiusAtReferenceResolution = 92f;
    [SerializeField, Min(0f)] float deadZoneAtReferenceResolution = 14f;
    [SerializeField] Color baseColor = new(1f, 1f, 1f, .18f);
    [SerializeField] Color knobColor = new(1f, 1f, 1f, .42f);

    int activeTouchId = -1;
    Vector2 anchor;
    Vector2 knob;
    Vector2 value;
    Texture2D circleTexture;
    Sprite circleSprite;
    GameObject visualRoot;
    RectTransform baseRect;
    RectTransform knobRect;
    bool visualsVisible;

    public bool IsTouchActive => activeTouchId >= 0;
    public Vector2 Value => value;

    void Awake()
    {
        EnsureVisuals();
#if UNITY_WEBGL && !UNITY_EDITOR
        InitializeBrowserTouchCleanup();
#endif
    }

    // WebGL can finish creating its canvas after Awake. The JavaScript side is
    // idempotent, so retrying from Start guarantees its release/cancel listeners exist.
    void Start()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        InitializeBrowserTouchCleanup();
#endif
    }

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
        Touch.onFingerUp += HandleFingerUp;
    }

    void OnDisable()
    {
        Touch.onFingerUp -= HandleFingerUp;
        Release();
        EnhancedTouchSupport.Disable();
    }

    void OnDestroy()
    {
        if (circleSprite != null) Destroy(circleSprite);
        if (circleTexture != null) Destroy(circleTexture);
    }

    void Update()
    {
        if (activeTouchId < 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // Consume a stale browser release before considering a later touch.
            LootGoblinConsumeBrowserTouchRelease();
#endif
            CaptureNewTouch();
            return;
        }

        foreach (Touch touch in Touch.activeTouches)
        {
            if (touch.touchId != activeTouchId) continue;
            if (touch.phase is UnityEngine.InputSystem.TouchPhase.Ended or UnityEngine.InputSystem.TouchPhase.Canceled)
            {
                Release();
                return;
            }

            UpdateValue(touch.screenPosition);
            return;
        }

        // Ended/cancelled touches can leave the active-touch list before Update.
#if UNITY_WEBGL && !UNITY_EDITOR
        // Browser pointer-capture notifications can arrive before Unity reports the
        // matching touch phase. The active-touch scan above is authoritative while the
        // gesture is live; consume browser cleanup only after it disappears.
        LootGoblinConsumeBrowserTouchRelease();
#endif
        Release();
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) Release();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused) Release();
    }

    void CaptureNewTouch()
    {
        Rect zone = GetActivationZone();
        foreach (Touch touch in Touch.activeTouches)
        {
            if (touch.phase != UnityEngine.InputSystem.TouchPhase.Began || !zone.Contains(touch.screenPosition)) continue;
            activeTouchId = touch.touchId;
            anchor = knob = touch.screenPosition;
            value = Vector2.zero;
            visualsVisible = true;
            UpdateVisuals();
            visualRoot.SetActive(true);
            return;
        }
    }

    void UpdateValue(Vector2 screenPosition)
    {
        float scale = ReferenceScale(Screen.width, Screen.height);
        float radius = radiusAtReferenceResolution * scale;
        float deadZone = deadZoneAtReferenceResolution * scale;
        knob = anchor + Vector2.ClampMagnitude(screenPosition - anchor, radius);
        value = CalculateValue(anchor, screenPosition, radius, deadZone);
        UpdateVisuals();
    }

    void Release()
    {
        activeTouchId = -1;
        anchor = knob = Vector2.zero;
        value = Vector2.zero;
        visualsVisible = false;
        if (visualRoot != null) visualRoot.SetActive(false);
    }

    // Called by the WebGL canvas when Safari ends a gesture outside the canvas bounds.
    [UnityEngine.Scripting.Preserve]
    public void ReleaseFromBrowser() => Release();

    void HandleFingerUp(Finger finger)
    {
        if (!IsTouchActive || finger.lastTouch.touchId != activeTouchId) return;
        Release();
    }

    Rect GetActivationZone() => CalculateActivationZone(Screen.width, Screen.height, Screen.safeArea, zoneWidth, zoneTop);

    // Keep the joystick in a retained screen-space overlay so it stays independent of
    // the responsive full-bleed gameplay camera framing.
    void EnsureVisuals()
    {
        if (visualRoot != null) return;

        EnsureCircleTexture();
        circleSprite = Sprite.Create(circleTexture, new Rect(0, 0, circleTexture.width, circleTexture.height), new Vector2(.5f, .5f));
        visualRoot = new GameObject("Floating Joystick", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        visualRoot.transform.SetParent(transform, false);

        var canvas = visualRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = -100;
        var rootRect = visualRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
        visualRoot.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        baseRect = CreateCircleVisual("Base", baseColor);
        knobRect = CreateCircleVisual("Knob", knobColor);
        visualRoot.SetActive(false);
    }

    RectTransform CreateCircleVisual(string visualName, Color color)
    {
        var visual = new GameObject(visualName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = visual.GetComponent<RectTransform>();
        rect.SetParent(visualRoot.transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        var image = visual.GetComponent<Image>();
        image.sprite = circleSprite;
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    void UpdateVisuals()
    {
        if (!visualsVisible || baseRect == null || knobRect == null) return;

        float radius = radiusAtReferenceResolution * ReferenceScale(Screen.width, Screen.height);
        float baseDiameter = radius * 2f;
        float knobDiameter = baseDiameter * .56f;
        Vector2 screenCenter = new(Screen.width * .5f, Screen.height * .5f);
        baseRect.anchoredPosition = anchor - screenCenter;
        baseRect.sizeDelta = new Vector2(baseDiameter, baseDiameter);
        knobRect.anchoredPosition = knob - screenCenter;
        knobRect.sizeDelta = new Vector2(knobDiameter, knobDiameter);
    }

    void EnsureCircleTexture()
    {
        if (circleTexture != null) return;

        const int size = 64;
        circleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Runtime Joystick Circle",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float distance = Vector2.Distance(new Vector2(x, y), new Vector2((size - 1) * .5f, (size - 1) * .5f));
            pixels[y * size + x] = distance <= size * .5f ? Color.white : Color.clear;
        }
        circleTexture.SetPixels32(pixels);
        circleTexture.Apply(false, true);
    }

    public static Rect CalculateActivationZone(int screenWidth, int screenHeight, Rect safeArea, float widthFraction = DefaultZoneWidth, float topFraction = DefaultZoneTop)
    {
        Rect safe = Rect.MinMaxRect(
            Mathf.Clamp(safeArea.xMin, 0, screenWidth),
            Mathf.Clamp(safeArea.yMin, 0, screenHeight),
            Mathf.Clamp(safeArea.xMax, 0, screenWidth),
            Mathf.Clamp(safeArea.yMax, 0, screenHeight));
        if (safe.width <= 0 || safe.height <= 0) safe = new Rect(0, 0, screenWidth, screenHeight);

        return Rect.MinMaxRect(safe.xMin, safe.yMin, safe.xMin + safe.width * widthFraction, safe.yMin + safe.height * topFraction);
    }

    public static Vector2 CalculateValue(Vector2 anchor, Vector2 screenPosition, float radius, float deadZone)
    {
        Vector2 offset = screenPosition - anchor;
        float distance = offset.magnitude;
        if (distance <= deadZone || radius <= deadZone) return Vector2.zero;

        float magnitude = Mathf.Clamp01((distance - deadZone) / (radius - deadZone));
        return offset / distance * magnitude;
    }

    static float ReferenceScale(int screenWidth, int screenHeight) => Mathf.Clamp(Mathf.Min(screenWidth / 540f, screenHeight / 960f), .8f, 2f);

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern void LootGoblinDisableBrowserTouchGestures();

    [DllImport("__Internal")]
    static extern int LootGoblinConsumeBrowserTouchRelease();

    void InitializeBrowserTouchCleanup() => LootGoblinDisableBrowserTouchGestures();
#endif
}
