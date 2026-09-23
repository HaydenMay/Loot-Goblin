using UnityEngine;

public enum ProductionWeaponType
{
    Hammer,
    Sword,
    Dagger,
    Other
}

/// <summary>Small data boundary for selecting a weapon prefab and its animation set.</summary>
[CreateAssetMenu(menuName = "Loot Goblin/Production/Weapon Definition", fileName = "WeaponDefinition")]
public sealed class ProductionWeaponDefinition : ScriptableObject
{
    [SerializeField] ProductionWeaponType weaponType;
    [SerializeField] GameObject weaponPrefab;
    [SerializeField] RuntimeAnimatorController animatorController;
    [SerializeField] string attackStateName = "Hammer_Overhead_Smash_Test";
    [SerializeField] Vector3 socketLocalPosition;
    [SerializeField] Vector3 socketLocalEulerAngles;
    [SerializeField] Vector3 socketLocalScale = Vector3.one;

    public ProductionWeaponType WeaponType => weaponType;
    public GameObject WeaponPrefab => weaponPrefab;
    public RuntimeAnimatorController AnimatorController => animatorController;
    public string AttackStateName => attackStateName;
    public Vector3 SocketLocalPosition => socketLocalPosition;
    public Quaternion SocketLocalRotation => Quaternion.Euler(socketLocalEulerAngles);
    public Vector3 SocketLocalScale => socketLocalScale;
}
