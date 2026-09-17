using UnityEngine;

// A compact, sword-specific in-place sweep. Combat still owns targeting, cooldown and damage;
// this component only moves the existing right arm so the hand-parented sword follows naturally.
[DisallowMultipleComponent]
public sealed class GoblinSwordAttack : MonoBehaviour
{
    [SerializeField] int damage = 1;
    [SerializeField] float knockbackForce = 4.5f;
    [SerializeField, Range(0f, 30f)] float rightArmReachDegrees = 14f;
    [SerializeField, Range(0f, 60f)] float backswingDegrees = 22f;
    [SerializeField, Range(0f, 220f)] float swingArcDegrees = 160f;
    [SerializeField, Min(.01f)] float backswingDuration = .06f;
    [SerializeField, Min(.01f)] float swingDuration = .10f;
    [SerializeField, Min(.01f)] float recoveryDuration = .14f;

    Animator animator;
    Transform rightUpperArm;
    Transform rightLowerArm;
    Transform rightHand;
    Quaternion rightUpperArmRest;
    Quaternion rightLowerArmRest;
    Quaternion rightHandRest;
    float elapsed;
    float sweepDirection;
    bool initialized;
    bool swinging;

    public int Damage => damage;
    public float KnockbackForce => knockbackForce;
    public float Duration => backswingDuration + swingDuration + recoveryDuration;
    public bool IsSwinging => swinging;
    public bool HitThisTick { get; private set; }

    public void Configure(Animator source)
    {
        animator = source;
        initialized = false;
        CacheBones();
    }

    void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        CacheBones();
    }

    public void Play()
    {
        if (swinging || !EnsureInitialized()) return;
        elapsed = 0;
        sweepDirection = FindFrontSweepDirection();
        swinging = true;
        HitThisTick = false;
        ApplyPose(0);
    }

    // LootGoblinRun owns the deterministic combat tick, so its damage marker shares this timing.
    public void Tick(float dt)
    {
        HitThisTick = false;
        if (!swinging) return;

        float previous = elapsed;
        elapsed = Mathf.Min(Duration, elapsed + Mathf.Max(0, dt));
        float hitTime = backswingDuration + swingDuration * .58f;
        if (previous < hitTime && elapsed >= hitTime) HitThisTick = true;

        ApplyPose(elapsed);
        if (elapsed >= Duration)
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

    void LateUpdate()
    {
        if (swinging) ApplyPose(elapsed);
        else if (initialized) CaptureRestPose();
    }

    bool EnsureInitialized()
    {
        if (!initialized) CacheBones();
        return initialized;
    }

    void CacheBones()
    {
        if (animator == null || !animator.isHuman) return;

        rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        initialized = rightUpperArm != null && rightLowerArm != null && rightHand != null;
        if (initialized) CaptureRestPose();
        else Debug.LogError("Goblin sword attack needs the humanoid right upper arm, forearm, and hand bones.", this);
    }

    void CaptureRestPose()
    {
        rightUpperArmRest = rightUpperArm.localRotation;
        rightLowerArmRest = rightLowerArm.localRotation;
        rightHandRest = rightHand.localRotation;
    }

    void ApplyPose(float time)
    {
        if (!initialized) return;

        float arcAngle;
        float reachWeight;
        if (time < backswingDuration)
        {
            float t = Smooth(time / backswingDuration);
            arcAngle = Mathf.Lerp(0, -backswingDegrees, t);
            reachWeight = t;
        }
        else if (time < backswingDuration + swingDuration)
        {
            float t = Smooth((time - backswingDuration) / swingDuration);
            arcAngle = Mathf.Lerp(-backswingDegrees, swingArcDegrees - backswingDegrees, t);
            reachWeight = 1;
        }
        else
        {
            float t = Smooth((time - backswingDuration - swingDuration) / recoveryDuration);
            arcAngle = Mathf.Lerp(swingArcDegrees - backswingDegrees, 0, t);
            reachWeight = 1 - t;
        }

        // The sword is fixed X -90 under the hand. Rotate the complete arm chain around the
        // goblin's upright axis so its path is right -> front -> left in character space,
        // rather than an arbitrary local-bone twist that reads as a front-left poke.
        Quaternion armRestWorld = rightUpperArm.parent.rotation * rightUpperArmRest;
        Quaternion reachRotation = Quaternion.AngleAxis(-rightArmReachDegrees * reachWeight, transform.right);
        Quaternion sweepRotation = Quaternion.AngleAxis(sweepDirection * arcAngle, transform.up);
        rightUpperArm.rotation = sweepRotation * reachRotation * armRestWorld;
        rightLowerArm.localRotation = rightLowerArmRest;
        rightHand.localRotation = rightHandRest;
    }

    float FindFrontSweepDirection()
    {
        Vector3 armDirection = Vector3.ProjectOnPlane(rightHand.position - rightUpperArm.position, transform.up);
        if (armDirection.sqrMagnitude < .0001f) return -1;

        // Pick the rotation sign that moves the hand from its right-side ready position toward
        // the goblin's forward/front direction before it continues across to the left.
        float towardFront = Vector3.Dot(Vector3.Cross(transform.up, armDirection.normalized), transform.forward);
        return towardFront >= 0 ? 1 : -1;
    }

    void ResetToReady()
    {
        if (!initialized) return;
        rightUpperArm.localRotation = rightUpperArmRest;
        rightLowerArm.localRotation = rightLowerArmRest;
        rightHand.localRotation = rightHandRest;
    }

    static float Smooth(float t) => Mathf.SmoothStep(0, 1, Mathf.Clamp01(t));
}
