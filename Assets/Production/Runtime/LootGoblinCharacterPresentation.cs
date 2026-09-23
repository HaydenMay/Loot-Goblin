using UnityEngine;

/// <summary>Visual-only Loot Goblin rig shared by the player and future character previews.</summary>
[DisallowMultipleComponent]
public sealed class LootGoblinCharacterPresentation : MonoBehaviour
{
    [SerializeField] Animator animator;
    [SerializeField] Transform weaponSocket;

    public Animator Animator => animator;
    public Transform WeaponSocket => weaponSocket;

    void Reset() => CacheReferences();

    void OnValidate()
    {
        if (animator == null || weaponSocket == null)
            CacheReferences();
    }

    public void PrepareForPreview(bool animateWhenOffscreen = true)
    {
        if (animator == null) return;
        animator.applyRootMotion = false;
        animator.cullingMode = animateWhenOffscreen
            ? AnimatorCullingMode.AlwaysAnimate
            : AnimatorCullingMode.CullUpdateTransforms;
    }

    public void SetAnimationController(RuntimeAnimatorController controller)
    {
        if (animator != null && controller != null)
            animator.runtimeAnimatorController = controller;
    }

    void CacheReferences()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (weaponSocket == null)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "WeaponSocket_R")
                {
                    weaponSocket = child;
                    break;
                }
            }
        }
    }
}
