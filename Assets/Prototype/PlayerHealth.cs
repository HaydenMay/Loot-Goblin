using UnityEngine;

// Run-local player vitality. LootGoblinRun owns when a run resets; this component owns damage timing.
public sealed class PlayerHealth : MonoBehaviour
{
    const int BaseMaximumHealth = 100;
    const float InvulnerabilityDuration = .75f;
    const float PunchDuration = .12f;

    float invulnerabilityRemaining;
    float punchRemaining;
    Vector3 restingScale;

    int equipmentHealthBonus;
    public int Current { get; private set; } = BaseMaximumHealth;
    public int Max => BaseMaximumHealth + equipmentHealthBonus;
    public bool IsDead => Current <= 0;
    public bool IsInvulnerable => invulnerabilityRemaining > 0;

    void Awake()
    {
        restingScale = transform.localScale;
    }

    public void ResetHealth()
    {
        Current = Max;
        invulnerabilityRemaining = 0;
        punchRemaining = 0;
        transform.localScale = restingScale;
    }

    // Equipment owns the bonus value; health keeps its damage/invulnerability lifecycle.
    public void SetEquipmentHealthBonus(int bonus)
    {
        equipmentHealthBonus = Mathf.Max(0, bonus);
        Current = Mathf.Min(Current, Max);
    }

    // Returns false when this hit was ignored by the short post-hit safety window.
    public bool TryTakeDamage(int amount)
    {
        if (amount <= 0 || IsDead || IsInvulnerable) return false;

        Current = Mathf.Max(0, Current - amount);
        invulnerabilityRemaining = InvulnerabilityDuration;
        punchRemaining = PunchDuration;
        return true;
    }

    public void Tick(float dt)
    {
        invulnerabilityRemaining = Mathf.Max(0, invulnerabilityRemaining - dt);
        punchRemaining = Mathf.Max(0, punchRemaining - dt);

        // One small scale punch is enough feedback for this prototype and adds no shared hit system.
        float t = punchRemaining / PunchDuration;
        transform.localScale = restingScale * (1 + Mathf.Sin(t * Mathf.PI) * .12f);
    }
}
