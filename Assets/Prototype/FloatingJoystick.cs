using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// Owns one forgiving, lower-left touch gesture and presents it as a movement vector.
/// The gameplay owner decides how that vector affects the player.
/// </summary>
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

    public bool IsTouchActive => activeTouchId >= 0;
    public Vector2 Value => value;

    void Awake()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        LootGoblinDisableBrowserTouchGestures();
#endif
    }

    void OnEnable() => EnhancedTouchSupport.Enable();

    void OnDisable()
    {
        Release();
        EnhancedTouchSupport.Disable();
    }

    void OnDestroy()
    {
        if (circleTexture != null) Destroy(circleTexture);
    }

    void Update()
    {
        if (activeTouchId < 0)
        {
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

        // Ended touches can leave the active-touch list before this frame's Update.
        Release();
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
    }

    void Release()
    {
        activeTouchId = -1;
        value = Vector2.zero;
    }

    Rect GetActivationZone() => CalculateActivationZone(Screen.width, Screen.height, Screen.safeArea, zoneWidth, zoneTop);

    void OnGUI()
    {
        if (!IsTouchActive || Event.current.type != EventType.Repaint) return;

        EnsureCircleTexture();
        float radius = radiusAtReferenceResolution * ReferenceScale(Screen.width, Screen.height);
        float diameter = radius * 2f;
        DrawCircle(anchor, diameter, baseColor);
        DrawCircle(knob, diameter * .56f, knobColor);
    }

    void DrawCircle(Vector2 screenPosition, float diameter, Color color)
    {
        float y = Screen.height - screenPosition.y;
        Rect rect = new(screenPosition.x - diameter * .5f, y - diameter * .5f, diameter, diameter);
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, circleTexture, ScaleMode.StretchToFill, true);
        GUI.color = previous;
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
#endif
}
