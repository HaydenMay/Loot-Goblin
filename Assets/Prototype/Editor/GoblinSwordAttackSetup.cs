using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GoblinSwordAttackSetup
{
    const string ScenePath = "Assets/Scenes/LootGoblin.unity";
    const string SwordPath = "Assets/Equipment/Weapons/Swords/Sword.fbx";

    [MenuItem("Loot Goblin/Setup Sword Attack")]
    public static void Setup()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var run = Object.FindAnyObjectByType<LootGoblinRun>();
        if (run == null) throw new System.InvalidOperationException("LootGoblinRun was not found in the playable scene.");

        var animator = run.Player.GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman) throw new System.InvalidOperationException("The playable goblin needs its existing humanoid Animator.");
        Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (hand == null) throw new System.InvalidOperationException("The playable goblin has no humanoid right-hand bone.");

        Transform sword = FindSword(run.Player);
        if (sword == null)
        {
            var swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwordPath);
            if (swordPrefab == null) throw new System.InvalidOperationException("The imported Sword.fbx could not be loaded.");
            sword = ((GameObject)PrefabUtility.InstantiatePrefab(swordPrefab, scene)).transform;
            sword.name = "Sword";
            sword.SetParent(hand, false);
            // The source scene did not retain the previous hand attachment. These values restore
            // a compact, hand-held presentation for this imported model; the sword has no scripts.
            sword.localPosition = new Vector3(0, .00025f, .0004f);
            sword.localScale = Vector3.one * .18f;
        }
        else if (sword.parent != hand)
        {
            // Keep an already-imported sword in the existing right-hand chain rather than
            // creating or animating a second weapon object.
            Undo.SetTransformParent(sword, hand, "Attach Sword To Goblin Right Hand");
        }

        // This FBX must sit X -90, Y -90 under the existing right-hand socket. GoblinSwordAttack only
        // rotates that arm chain; it never adds animation to the sword transform itself.
        sword.localRotation = Quaternion.Euler(-90, -90, 0);

        var attack = run.Player.GetComponent<GoblinSwordAttack>();
        if (attack == null) attack = Undo.AddComponent<GoblinSwordAttack>(run.Player.gameObject);
        attack.Configure(animator);
        EditorUtility.SetDirty(attack);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Loot Goblin: sword attack setup complete. Sword is parented under the goblin right hand.");
    }

    public static void SetupAndValidate()
    {
        Setup();
        LootGoblinPrototypeEditor.ValidateBatch();
    }

    static Transform FindSword(Transform root)
    {
        foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
            if (candidate.name == "Sword") return candidate;
        return null;
    }
}
