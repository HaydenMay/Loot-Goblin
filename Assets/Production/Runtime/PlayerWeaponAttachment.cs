using UnityEngine;

/// <summary>Attaches the selected weapon prefab to the canonical character's right-hand socket.</summary>
[DisallowMultipleComponent]
public sealed class PlayerWeaponAttachment : MonoBehaviour
{
    [SerializeField] LootGoblinCharacterPresentation presentation;
    [SerializeField] Animator animator;
    [SerializeField] ProductionWeaponDefinition initialWeapon;

    GameObject equippedInstance;
    ProductionWeaponDefinition equippedDefinition;

    public ProductionWeaponDefinition EquippedDefinition => equippedDefinition;
    public ProductionWeaponType? EquippedWeaponType => equippedDefinition == null
        ? null
        : equippedDefinition.WeaponType;
    public string AttackStateName => equippedDefinition == null
        ? "Hammer_Overhead_Smash_Test"
        : equippedDefinition.AttackStateName;
    public GameObject EquippedInstance => equippedInstance;

    void Awake()
    {
        if (presentation == null) presentation = GetComponentInChildren<LootGoblinCharacterPresentation>();
        if (animator == null && presentation != null) animator = presentation.Animator;
        if (initialWeapon != null) Equip(initialWeapon);
    }

    public bool Equip(ProductionWeaponDefinition definition)
    {
        if (definition == null || definition.WeaponPrefab == null ||
            presentation == null || presentation.WeaponSocket == null)
        {
            Debug.LogError("Cannot equip a production weapon: its definition, prefab, presentation, or WeaponSocket_R is missing.", this);
            return false;
        }

        Transform socket = presentation.WeaponSocket;
        var nextInstance = Instantiate(definition.WeaponPrefab, socket, false);
        nextInstance.name = definition.WeaponPrefab.name;
        nextInstance.transform.localPosition = definition.SocketLocalPosition;
        nextInstance.transform.localRotation = definition.SocketLocalRotation;
        nextInstance.transform.localScale = definition.SocketLocalScale;

        if (equippedInstance != null) Destroy(equippedInstance);
        equippedInstance = nextInstance;
        equippedDefinition = definition;

        if (animator == null) animator = presentation.Animator;
        if (animator != null && definition.AnimatorController != null)
            animator.runtimeAnimatorController = definition.AnimatorController;

        return true;
    }
}
