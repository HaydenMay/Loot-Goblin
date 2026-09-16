using UnityEngine;

// Presentation and hop timing only; the run still owns health, steering and loot.
public sealed class SlimeMotion : MonoBehaviour, IHitReceiver
{
    [SerializeField] Transform visual;
    [SerializeField] float knockbackDamping = 14f;
    [SerializeField] float hitReactionDuration = .12f;
    float time, attackTime = -1, attackCooldown, deathTime;
    float hitReactionRemaining;
    Vector3 knockbackVelocity;
    bool dying;
    public bool DeathFinished => dying && deathTime >= .32f;
    public bool IsWindingUp => attackTime >= 0 && attackTime < .28f;
    public bool IsRecoiling => knockbackVelocity.sqrMagnitude > .0001f;
    // True only on the frame the existing lunge begins.
    public bool AttackHitThisTick { get; private set; }

    public void BeginDeath() { dying = true; deathTime = 0; }

    public void TakeHit(int damage, Vector3 hitDirection, float knockbackForce)
    {
        if (dying || damage <= 0 || knockbackForce <= 0) return;

        hitDirection.y = 0;
        if (hitDirection.sqrMagnitude < .0001f) return;

        knockbackVelocity += hitDirection.normalized * knockbackForce;
        hitReactionRemaining = hitReactionDuration;
    }

    // Steering remains owned by LootGoblinRun; this component owns the temporary hit motion it adds.
    public Vector3 ConsumeKnockback(float dt)
    {
        Vector3 displacement = knockbackVelocity * dt;
        knockbackVelocity = Vector3.MoveTowards(knockbackVelocity, Vector3.zero, knockbackDamping * dt);
        return displacement;
    }

    // Returns the fraction of normal steering speed during this hop.
    public float Tick(float dt, bool approaching)
    {
        AttackHitThisTick = false;
        time += dt;
        hitReactionRemaining = Mathf.Max(0, hitReactionRemaining - dt);
        if (dying)
        {
            deathTime += dt;
            float t = Mathf.Clamp01(deathTime / .32f);
            Pose(Mathf.Lerp(1, .08f, t), Mathf.Lerp(1, .02f, t), 0, 0);
            return 0;
        }
        attackCooldown -= dt;
        if (attackTime < 0 && !approaching && attackCooldown <= 0) attackTime = 0;
        if (attackTime >= 0)
        {
            float previousAttackTime = attackTime;
            attackTime += dt;
            if (previousAttackTime < .28f && attackTime >= .28f) AttackHitThisTick = true;
            if (attackTime < .28f)
            {
                float t = attackTime / .28f;
                Pose(1 + .25f*t, 1 - .4f*t, 0, 0);
            }
            else
            {
                float t = Mathf.Clamp01((attackTime - .28f) / .24f);
                float bump = Mathf.Sin(t * Mathf.PI);
                Pose(1 - .12f*bump, 1 + .2f*bump, .08f*bump, .34f*bump);
            }
            if (attackTime >= .52f) { attackTime = -1; attackCooldown = .7f; }
            return 0;
        }
        if (!approaching)
        {
            float wobble = Mathf.Sin(time * 5) * .045f;
            Pose(1 + wobble, 1 - wobble, 0, 0);
            return 0;
        }
        float phase = Mathf.Repeat(time, .6f) / .6f;
        if (phase < .25f)
        {
            float squash = Mathf.Sin(phase / .25f * Mathf.PI);
            Pose(1 + .18f*squash, 1 - .25f*squash, 0, 0);
            return 0;
        }
        float hop = Mathf.Sin((phase - .25f) / .75f * Mathf.PI);
        Pose(1 - .1f*hop, 1 + .15f*hop, .32f*hop, 0);
        return hop * 2.1f;
    }

    void Pose(float width, float height, float lift, float forward)
    {
        // One brief recoil squash layers over the existing slime motion without replacing it.
        float recoil = hitReactionDuration <= 0 ? 0 : hitReactionRemaining / hitReactionDuration;
        width *= 1 + .16f * recoil;
        height *= 1 - .12f * recoil;
        visual.localScale = new Vector3(width, height, width);
        visual.localPosition = new Vector3(0, lift, forward);
    }
}
