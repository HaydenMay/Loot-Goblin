using UnityEngine;

// Owns the slime's presentation, hop timing and committed attack motion.
// LootGoblinRun still owns chase steering, arena collision, health and loot.
public sealed class SlimeMotion : MonoBehaviour, IHitReceiver
{
    public enum AttackState
    {
        Chase,
        AttackWindup,
        AttackLunge,
        AttackRecovery,
        Dead
    }

    [Header("Visual")]
    [SerializeField] Transform visual;
    [SerializeField] float trailingThicknessStart = 1f;
    [SerializeField] float trailingThicknessEnd = 0.5f;
    [SerializeField, Range(.35f, .9f)] float stretchedBodyWidth = .62f;

    [Header("Attack")]
    [SerializeField, Min(.5f)] float attackRange = 2f;
    [SerializeField, Min(.01f)] float windupDuration = .8f;
    [SerializeField, Min(.01f)] float lungeDuration = .3f;
    [SerializeField, Min(.5f)] float maximumLandingDistance = 2.65f;
    [SerializeField, Min(.01f)] float recoveryDuration = .5f;
    [SerializeField, Range(0, 1)] float damageWindowStart = .35f;
    [SerializeField, Min(.05f)] float attackHitRadius = .85f;

    [Header("Hit Reaction")]
    [SerializeField] float knockbackDamping = 14f;
    [SerializeField] float hitReactionDuration = .12f;

    Transform body;
    Transform leftEye;
    Transform rightEye;
    Mesh runtimeBodyMesh;
    Bounds bodyMeshBounds;
    Vector3[] bodyVertices;
    Vector3[] stretchedVertices;
    Vector3 bodyPosition;
    Vector3 bodyScale;
    Vector3 leftEyePosition;
    Vector3 rightEyePosition;
    Vector3 leftEyeScale;
    Vector3 rightEyeScale;
    Vector3 knockbackVelocity;
    Vector3 lungeStart;
    Vector3 landingPosition;
    Vector3 previousAttackFrontPosition;
    Vector3 attackFrontPosition;
    float time;
    float stateTime;
    float deathTime;
    float hitReactionRemaining;
    bool damageConsumed;
    bool damageWindowActiveThisTick;

    public AttackState CurrentState { get; private set; } = AttackState.Chase;
    public bool DeathFinished => CurrentState == AttackState.Dead && deathTime >= .32f;
    public bool IsWindingUp => CurrentState == AttackState.AttackWindup;
    public bool IsLunging => CurrentState == AttackState.AttackLunge;
    public bool CanTrackTarget => CurrentState == AttackState.Chase || CurrentState == AttackState.AttackWindup;
    public bool ReadyToCommit => CurrentState == AttackState.AttackWindup && stateTime >= windupDuration;
    public bool IsRecoiling => knockbackVelocity.sqrMagnitude > .0001f;
    public float AttackRange => attackRange;
    public float MaximumLandingDistance => maximumLandingDistance;
    public float AttackHitRadius => attackHitRadius;
    public Vector3 PreviousAttackFrontPosition => previousAttackFrontPosition;
    public Vector3 AttackFrontPosition => attackFrontPosition;

    void Awake()
    {
        CacheVisualParts();
    }

    void CacheVisualParts()
    {
        if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
        if (visual == null) return;

        body = visual.Find("Body");
        leftEye = visual.Find("Left Eye");
        rightEye = visual.Find("Right Eye");
        if (body != null)
        {
            bodyPosition = body.localPosition;
            bodyScale = body.localScale;
            var filter = body.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                runtimeBodyMesh = Instantiate(filter.sharedMesh);
                runtimeBodyMesh.name = filter.sharedMesh.name + " (Slime Runtime)";
                filter.sharedMesh = runtimeBodyMesh;
                bodyMeshBounds = runtimeBodyMesh.bounds;
                bodyVertices = runtimeBodyMesh.vertices;
                stretchedVertices = new Vector3[bodyVertices.Length];
            }
        }
        if (leftEye != null)
        {
            leftEyePosition = leftEye.localPosition;
            leftEyeScale = leftEye.localScale;
        }
        if (rightEye != null)
        {
            rightEyePosition = rightEye.localPosition;
            rightEyeScale = rightEye.localScale;
        }
    }

    void OnDestroy()
    {
        if (runtimeBodyMesh == null) return;
        if (Application.isPlaying) Destroy(runtimeBodyMesh);
        else DestroyImmediate(runtimeBodyMesh);
    }

    public void BeginDeath()
    {
        if (CurrentState == AttackState.Dead) return;
        CurrentState = AttackState.Dead;
        deathTime = 0;
        ResetStretchVisual();
    }

    public bool CanBeginAttack(float distance)
    {
        return CurrentState == AttackState.Chase && distance <= attackRange;
    }

    public void BeginAttackWindup()
    {
        if (CurrentState != AttackState.Chase) return;
        CurrentState = AttackState.AttackWindup;
        stateTime = 0;
        damageConsumed = false;
    }

    public void CommitLunge(Vector3 targetLandingPosition)
    {
        if (!ReadyToCommit) return;

        lungeStart = transform.position;
        landingPosition = targetLandingPosition;
        landingPosition.y = lungeStart.y;
        Vector3 offset = landingPosition - lungeStart;
        offset.y = 0;
        if (offset.magnitude > maximumLandingDistance)
            landingPosition = lungeStart + offset.normalized * maximumLandingDistance;
        if (offset.sqrMagnitude > .0001f) transform.forward = offset;

        CurrentState = AttackState.AttackLunge;
        stateTime = 0;
        previousAttackFrontPosition = attackFrontPosition = lungeStart;
    }

    public bool TryConsumeAttackDamage()
    {
        if (!damageWindowActiveThisTick || damageConsumed) return false;
        damageConsumed = true;
        return true;
    }

    public void TakeHit(int damage, Vector3 hitDirection, float knockbackForce)
    {
        if (CurrentState == AttackState.Dead || damage <= 0 || knockbackForce <= 0) return;

        hitDirection.y = 0;
        if (hitDirection.sqrMagnitude < .0001f) return;

        // Player hits can interrupt an attack.
        // "Committed" only means the slime AI cannot cancel it itself.
        InterruptAttackFromHit();

        knockbackVelocity += hitDirection.normalized * knockbackForce;
        hitReactionRemaining = hitReactionDuration;
    }

    // Chase steering remains owned by LootGoblinRun; this supplies its temporary recoil displacement.
    public Vector3 ConsumeKnockback(float dt)
    {
        Vector3 displacement = knockbackVelocity * dt;
        knockbackVelocity = Vector3.MoveTowards(
            knockbackVelocity,
            Vector3.zero,
            knockbackDamping * dt);

        return displacement;
    }

    void InterruptAttackFromHit()
    {
        if (CurrentState != AttackState.AttackWindup &&
            CurrentState != AttackState.AttackLunge &&
            CurrentState != AttackState.AttackRecovery)
            return;

        CurrentState = AttackState.Chase;
        stateTime = 0f;

        damageConsumed = false;
        damageWindowActiveThisTick = false;

        previousAttackFrontPosition = transform.position;
        attackFrontPosition = transform.position;

        ResetStretchVisual();
        Pose(1f, 1f, 0f);
    }
    // Returns the fraction of normal chase steering speed during the cute hop.
    public float Tick(float dt, bool approaching)
    {
        damageWindowActiveThisTick = false;
        time += dt;
        hitReactionRemaining = Mathf.Max(0, hitReactionRemaining - dt);

        switch (CurrentState)
        {
            case AttackState.Dead:
                deathTime += dt;
                float deathProgress = Mathf.Clamp01(deathTime / .32f);
                Pose(Mathf.Lerp(1, .08f, deathProgress), Mathf.Lerp(1, .02f, deathProgress), 0);
                return 0;

            case AttackState.AttackWindup:
                stateTime = Mathf.Min(windupDuration, stateTime + dt);
                float windup = Mathf.Clamp01(stateTime / windupDuration);
                windup = windup * windup * (3 - 2 * windup);
                Pose(1 + .34f * windup, 1 - .48f * windup, 0);
                return 0;

            case AttackState.AttackLunge:
                TickLunge(dt);
                return 0;

            case AttackState.AttackRecovery:
                stateTime += dt;
                float recovery = Mathf.Clamp01(stateTime / recoveryDuration);
                float settle = Mathf.Sin(recovery * Mathf.PI) * (1 - recovery);
                Pose(1 + .16f * settle, 1 - .2f * settle, 0);
                if (stateTime >= recoveryDuration)
                {
                    CurrentState = AttackState.Chase;
                    stateTime = 0;
                    Pose(1, 1, 0);
                }
                return 0;
        }

        if (!approaching)
        {
            float wobble = Mathf.Sin(time * 5) * .045f;
            Pose(1 + wobble, 1 - wobble, 0);
            return 0;
        }

        float phase = Mathf.Repeat(time, .6f) / .6f;
        if (phase < .25f)
        {
            float squash = Mathf.Sin(phase / .25f * Mathf.PI);
            Pose(1 + .18f * squash, 1 - .25f * squash, 0);
            return 0;
        }
        float hop = Mathf.Sin((phase - .25f) / .75f * Mathf.PI);
        Pose(1 - .1f * hop, 1 + .15f * hop, .32f * hop);
        return hop * 2.1f;
    }

    void TickLunge(float dt)
    {
        previousAttackFrontPosition = attackFrontPosition;
        stateTime += dt;
        float progress = Mathf.Clamp01(stateTime / lungeDuration);
        float currentTrailingThickness = Mathf.Lerp(trailingThicknessStart, trailingThicknessEnd, progress);
        float elasticProgress = 1 - Mathf.Pow(1 - progress, 1.3f);
        attackFrontPosition = Vector3.LerpUnclamped(lungeStart, landingPosition, elasticProgress);
        damageWindowActiveThisTick = progress >= damageWindowStart;
        ApplyStretchVisual(Vector3.Distance(lungeStart, attackFrontPosition), currentTrailingThickness);

        if (stateTime < lungeDuration) return;

        transform.position = landingPosition;
        previousAttackFrontPosition = attackFrontPosition = landingPosition;
        ResetStretchVisual();
        CurrentState = AttackState.AttackRecovery;
        stateTime = 0;
        Pose(1, 1, 0);
    }

    void ApplyStretchVisual(float frontDistance, float trailingThickness)
    {
        if (visual == null) return;
        visual.localScale = Vector3.one;
        visual.localPosition = Vector3.zero;

        if (runtimeBodyMesh != null && bodyVertices != null)
        {
            float halfDepth = Mathf.Max(.05f, bodyMeshBounds.extents.z * bodyScale.z);
            float totalLength = Mathf.Max(halfDepth, frontDistance + halfDepth);
            float minZ = bodyMeshBounds.min.z;
            float sizeZ = Mathf.Max(.0001f, bodyMeshBounds.size.z);
            for (int i = 0; i < bodyVertices.Length; i++)
            {
                Vector3 source = bodyVertices[i];
                float along = Mathf.Clamp01((source.z - minZ) / sizeZ);
                float tailToBody = Mathf.SmoothStep(trailingThickness, stretchedBodyWidth, Mathf.Clamp01(along / .58f));
                float head = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.45f, .72f, along));
                float radial = Mathf.Lerp(tailToBody, 1, head);
                float longitudinal = Mathf.SmoothStep(0, 1, Mathf.Pow(along, .35f));
                stretchedVertices[i] = new Vector3(source.x * radial, source.y * radial, longitudinal * totalLength / bodyScale.z);
            }
            runtimeBodyMesh.vertices = stretchedVertices;
            runtimeBodyMesh.RecalculateNormals();
            runtimeBodyMesh.RecalculateBounds();
        }

        float recoil = hitReactionDuration <= 0 ? 0 : hitReactionRemaining / hitReactionDuration;
        if (body != null)
        {
            body.localPosition = bodyPosition;
            body.localScale = new Vector3(bodyScale.x * (1 + .12f * recoil), bodyScale.y * (1 - .08f * recoil), bodyScale.z);
        }
        MoveEyeToHead(leftEye, leftEyePosition, leftEyeScale, frontDistance, recoil);
        MoveEyeToHead(rightEye, rightEyePosition, rightEyeScale, frontDistance, recoil);
    }

    static void MoveEyeToHead(Transform eye, Vector3 basePosition, Vector3 baseScale, float frontDistance, float recoil)
    {
        if (eye == null) return;
        eye.localPosition = new Vector3(basePosition.x, basePosition.y, frontDistance + basePosition.z);
        eye.localScale = new Vector3(baseScale.x * (1 + .12f * recoil), baseScale.y * (1 - .08f * recoil), baseScale.z);
    }

    void ResetStretchVisual()
    {
        if (runtimeBodyMesh != null && bodyVertices != null)
        {
            runtimeBodyMesh.vertices = bodyVertices;
            runtimeBodyMesh.RecalculateNormals();
            runtimeBodyMesh.RecalculateBounds();
        }
        if (body != null) { body.localPosition = bodyPosition; body.localScale = bodyScale; }
        if (leftEye != null) { leftEye.localPosition = leftEyePosition; leftEye.localScale = leftEyeScale; }
        if (rightEye != null) { rightEye.localPosition = rightEyePosition; rightEye.localScale = rightEyeScale; }
    }

    void Pose(float width, float height, float lift)
    {
        if (visual == null) return;
        float recoil = hitReactionDuration <= 0 ? 0 : hitReactionRemaining / hitReactionDuration;
        width *= 1 + .16f * recoil;
        height *= 1 - .12f * recoil;
        visual.localScale = new Vector3(width, height, width);
        visual.localPosition = new Vector3(0, lift, 0);
    }
}
