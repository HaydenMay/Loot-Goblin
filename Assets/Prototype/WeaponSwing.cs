using UnityEngine;

// Keeps the prototype weapon readable without requiring an Animator state or animation clip.
public sealed class WeaponSwing : MonoBehaviour
{
    [SerializeField] int damage = 1;
    [SerializeField] float knockbackForce = 4.5f;
    [SerializeField] float backswingDegrees = 22f;
    [SerializeField] float swingArcDegrees = 72f;
    [SerializeField] float backswingDuration = .06f;
    [SerializeField] float swingDuration = .10f;
    [SerializeField] float recoveryDuration = .14f;

    Vector3 readyPosition;
    Quaternion readyRotation;
    float elapsed;
    bool swinging;

    public int Damage => damage;
    public float KnockbackForce => knockbackForce;
    public bool IsSwinging => swinging;
    public bool HitThisTick { get; private set; }

    void Awake()
    {
        readyPosition = transform.localPosition;
        readyRotation = transform.localRotation;
        ResetToReady();
    }

    public void Play()
    {
        if (swinging) return;

        elapsed = 0;
        swinging = true;
        HitThisTick = false;
        ApplyPose(0);
    }

    // LootGoblinRun owns the prototype's deterministic game tick, so presentation shares it.
    public void Tick(float dt)
    {
        HitThisTick = false;
        if (!swinging) return;

        float previous = elapsed;
        elapsed = Mathf.Min(TotalDuration, elapsed + Mathf.Max(0, dt));
        float hitTime = backswingDuration + swingDuration * .58f;
        if (previous < hitTime && elapsed >= hitTime) HitThisTick = true;

        ApplyPose(elapsed);
        if (elapsed >= TotalDuration)
        {
            swinging = false;
            ResetToReady();
        }
    }

    public void Cancel()
    {
        swinging = false;
        HitThisTick = false;
        ResetToReady();
    }

    float TotalDuration => backswingDuration + swingDuration + recoveryDuration;

    void ApplyPose(float time)
    {
        float angle;
        if (time < backswingDuration)
        {
            angle = Mathf.Lerp(0, -backswingDegrees, Smooth(time / backswingDuration));
        }
        else if (time < backswingDuration + swingDuration)
        {
            float t = (time - backswingDuration) / swingDuration;
            angle = Mathf.Lerp(-backswingDegrees, swingArcDegrees - backswingDegrees, Smooth(t));
        }
        else
        {
            float t = (time - backswingDuration - swingDuration) / recoveryDuration;
            angle = Mathf.Lerp(swingArcDegrees - backswingDegrees, 0, Smooth(t));
        }

        var arcRotation = Quaternion.Euler(0, angle, 0);
        transform.localRotation = readyRotation * arcRotation;
        // Move the visual's centre around its holder so the rectangular placeholder traces an arc.
        transform.localPosition = arcRotation * readyPosition;
    }

    void ResetToReady()
    {
        transform.localPosition = readyPosition;
        transform.localRotation = readyRotation;
    }

    static float Smooth(float t) => Mathf.SmoothStep(0, 1, Mathf.Clamp01(t));
}
