using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>Project-local, narrowly scoped tooling for the production player foundation.</summary>
[InitializeOnLoad]
public static class ProductionFoundationBuilder
{
    const string ProbeRequest = "ProductionFoundation.probe";
    const string BuildRequest = "ProductionFoundation.build";
    const string RigPath = "Assets/Animations/LootGoblin_ProductionRig_Test.fbx";
    const string WalkSourcePath = "Assets/Resized_Goblin.fbx";
    const string PresentationPrefabPath = "Assets/Production/Prefabs/LootGoblinCharacterPresentation.prefab";
    const string PlayerPrefabPath = "Assets/Production/Prefabs/Player.prefab";
    const string HammerPrefabPath = "Assets/Production/Prefabs/TemporaryHammer.prefab";
    const string ControllerPath = "Assets/Production/Animation/Production_Player.controller";
    const string IdleClipPath = "Assets/Production/Animation/Idle_Loop.anim";
    const string WalkClipPath = "Assets/Production/Animation/Walk_Loop.anim";
    const string RunClipPath = "Assets/Production/Animation/Run_Loop.anim";
    const string AttackClipPath = "Assets/Production/Animation/Hammer_Overhead_Smash_Test.anim";
    const string HammerDefinitionPath = "Assets/Production/Weapons/Hammer_Overhead_Smash_Test.asset";
    const string GroundMaterialPath = "Assets/Production/Materials/ProductionGround.mat";
    const string ProductionScenePath = "Assets/Production/Scenes/DungeonRun_Production.unity";
    const string BuildLabScenePath = "Assets/Scenes/LootGoblin.unity";
    const float TargetCharacterHeight = 1.7f;

    static ProductionFoundationBuilder()
    {
        EditorApplication.update += PollRequests;
    }

    static void PollRequests()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string probePath = Path.Combine(projectRoot, ProbeRequest);
        string buildPath = Path.Combine(projectRoot, BuildRequest);

        if (File.Exists(probePath))
        {
            File.Delete(probePath);
            try { ProbeAssets(); }
            catch (Exception exception) { WriteResult("PROBE FAILED\n" + exception); }
        }

        if (File.Exists(buildPath))
        {
            File.Delete(buildPath);
            try { BuildProductionFoundation(); }
            catch (Exception exception) { WriteResult("BUILD FAILED\n" + exception); }
        }
    }

    [MenuItem("Loot Goblin/Production/Probe validated character assets")]
    public static void ProbeAssets()
    {
        var report = new StringBuilder();
        DescribeModel(report, "Assets/Animations/LootGoblin_ProductionRig_Test.fbx");
        DescribeModel(report, "Assets/Resized_Goblin.fbx");
        DescribeController(report, "Assets/Goblin_Controller.controller");
        WriteResult(report.ToString());
    }

    [MenuItem("Loot Goblin/Production/Build production foundation")]
    public static void BuildFromMenu()
    {
        BuildProductionFoundation();
    }

    public static void BuildForCommandLine()
    {
        try
        {
            BuildProductionFoundation();
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            WriteResult("BUILD FAILED\n" + exception);
            Debug.LogException(exception);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    static void DescribeModel(StringBuilder report, string path)
    {
        report.AppendLine($"MODEL {path}");
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        report.AppendLine(importer == null
            ? "  ModelImporter: missing"
            : $"  Rig: {importer.animationType}; optimize: {importer.optimizeGameObjects}; avatar setup: {importer.avatarSetup}");

        foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
            report.AppendLine($"  CLIP {clip.name}; length={clip.length:F3}; rate={clip.frameRate:F1}; looping={clip.isLooping}; wrap={clip.wrapMode}; legacy={clip.legacy}");

        foreach (var avatar in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>())
            report.AppendLine($"  AVATAR {avatar.name}; human={avatar.isHuman}; valid={avatar.isValid}");

        var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (source == null)
        {
            report.AppendLine("  Root prefab: missing");
            return;
        }

        var instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
        if (instance == null)
        {
            report.AppendLine("  Prefab instance: failed");
            return;
        }

        try
        {
            var animators = instance.GetComponentsInChildren<Animator>(true);
            report.AppendLine($"  Animators: {animators.Length}");
            foreach (var animator in animators)
                report.AppendLine($"    {GetPath(animator.transform)}; avatar={(animator.avatar == null ? "null" : animator.avatar.name)}");

            foreach (var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                report.AppendLine($"  SKIN {GetPath(renderer.transform)}; mesh={(renderer.sharedMesh == null ? "null" : renderer.sharedMesh.name)}; bones={renderer.bones.Length}; boundHeight={renderer.bounds.size.y:F3}; active={renderer.enabled}");
            }

            foreach (var transform in instance.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name.Contains("WeaponSocket_R") || transform.name.Contains("TEMP_Hammer"))
                {
                    report.AppendLine($"  NODE {GetPath(transform)}; localPos={transform.localPosition}; localRot={transform.localRotation.eulerAngles}; localScale={transform.localScale}; children={transform.childCount}");
                    for (int i = 0; i < transform.childCount; i++)
                    {
                        var child = transform.GetChild(i);
                        var filter = child.GetComponent<MeshFilter>();
                        var skinned = child.GetComponent<SkinnedMeshRenderer>();
                        var mesh = filter != null ? filter.sharedMesh : skinned != null ? skinned.sharedMesh : null;
                        var renderers = child.GetComponentsInChildren<Renderer>(true);
                        report.AppendLine($"    CHILD {child.name}; mesh={(mesh == null ? "none" : mesh.name)}; renderers={renderers.Length}; bound={(renderers.Length == 0 ? "none" : renderers[0].bounds.ToString())}");
                    }
                }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    static void DescribeController(StringBuilder report, string path)
    {
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
        report.AppendLine($"CONTROLLER {path}: {(controller == null ? "missing" : controller.name)}");
        if (controller == null) return;
        foreach (var clip in controller.animationClips)
            report.AppendLine($"  CLIP {clip.name}; length={clip.length:F3}; looping={clip.isLooping}; wrap={clip.wrapMode}");
    }

    static string GetPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            if (transform.parent != null) path = transform.name + "/" + path;
        }
        return path;
    }

    static void WriteResult(string result)
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/ProductionFoundationBuild.txt", result);
        Debug.Log(result);
    }

    static void BuildProductionFoundation()
    {
        string buildLabHash = File.Exists(BuildLabScenePath) ? HashFile(BuildLabScenePath) : "missing";

        EnsureFolder("Assets/Production");
        EnsureFolder("Assets/Production/Animation");
        EnsureFolder("Assets/Production/Materials");
        EnsureFolder("Assets/Production/Prefabs");
        EnsureFolder("Assets/Production/Scenes");
        EnsureFolder("Assets/Production/Weapons");

        var rigAsset = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
        if (rigAsset == null) throw new InvalidOperationException($"Corrected runtime rig was not found at {RigPath}.");
        var sourceIdle = FindClip(WalkSourcePath, "idle");
        var sourceWalk = FindClip(WalkSourcePath, "walk");
        var sourceAttack = FindClip(RigPath, "Hammer_Overhead_Smash_Test");
        if (sourceIdle == null || sourceWalk == null || sourceAttack == null)
            throw new InvalidOperationException($"Required source clips are missing. Idle={sourceIdle}, Walk={sourceWalk}, Attack={sourceAttack}.");

        var runSource = FindRunClip();
        var idleClip = CreateClipCopy(sourceIdle, IdleClipPath, loop: true);
        var walkClip = CreateClipCopy(sourceWalk, WalkClipPath, loop: true);
        var runClip = runSource == null ? null : CreateClipCopy(runSource, RunClipPath, loop: true);
        var attackClip = CreateClipCopy(sourceAttack, AttackClipPath, loop: false);
        var controller = CreateAnimatorController(idleClip, walkClip, runClip, attackClip);

        var rigAnimators = rigAsset.GetComponentsInChildren<Animator>(true);
        Avatar avatar = rigAnimators.Select(animator => animator.avatar)
            .FirstOrDefault(candidate => candidate != null && candidate.isHuman && candidate.isValid);
        if (avatar == null)
            avatar = AssetDatabase.LoadAllAssetsAtPath(RigPath).OfType<Avatar>()
                .FirstOrDefault(candidate => candidate.isHuman && candidate.isValid);
        if (avatar == null)
            throw new InvalidOperationException("The corrected runtime rig does not expose one valid Humanoid Avatar.");

        var rigInstance = PrefabUtility.InstantiatePrefab(rigAsset) as GameObject;
        if (rigInstance == null) throw new InvalidOperationException("The corrected runtime rig could not be instantiated.");

        ProductionWeaponDefinition weaponDefinition = null;
        GameObject presentationRoot = new GameObject("LootGoblinCharacterPresentation");
        try
        {
            var sourceHammer = FindDescendant(rigInstance.transform, "TEMP_Hammer_Overhead_Smash_Runtime");
            var socket = FindDescendant(rigInstance.transform, "WeaponSocket_R");
            if (sourceHammer == null || socket == null)
                throw new InvalidOperationException("The corrected rig must contain both WeaponSocket_R and its temporary hammer source node.");

            Vector3 hammerPosition = sourceHammer.localPosition;
            Vector3 hammerEuler = sourceHammer.localEulerAngles;
            Vector3 hammerScale = sourceHammer.localScale;
            CreateHammerPrefab(sourceHammer.gameObject);

            rigInstance.name = "Corrected Humanoid Runtime Rig";
            rigInstance.transform.SetParent(presentationRoot.transform, false);
            rigInstance.transform.localPosition = Vector3.zero;
            rigInstance.transform.localRotation = Quaternion.identity;

            Bounds initialBounds = GetRendererBounds(rigInstance);
            if (initialBounds.size.y < .05f)
                throw new InvalidOperationException($"Corrected rig has an unusable visual height: {initialBounds.size.y:F3}.");
            float visualScale = TargetCharacterHeight / initialBounds.size.y;
            rigInstance.transform.localScale = Vector3.one * visualScale;
            Vector3 rigLocalPosition = rigInstance.transform.localPosition;
            rigLocalPosition.y = -initialBounds.min.y * visualScale;
            rigInstance.transform.localPosition = rigLocalPosition;

            // The source mesh is retained in the validated FBX, but its test-only weapon child is
            // removed from this production presentation prefab before the reusable weapon is attached.
            sourceHammer = FindDescendant(rigInstance.transform, "TEMP_Hammer_Overhead_Smash_Runtime");
            if (sourceHammer == null) throw new InvalidOperationException("The temporary hammer source node disappeared unexpectedly.");
            UnityEngine.Object.DestroyImmediate(sourceHammer.gameObject);

            socket = FindDescendant(rigInstance.transform, "WeaponSocket_R");
            if (socket == null) throw new InvalidOperationException("WeaponSocket_R was lost while preparing the production presentation.");
            var animator = rigInstance.GetComponentsInChildren<Animator>(true).FirstOrDefault();
            if (animator == null) animator = rigInstance.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            var presentation = presentationRoot.AddComponent<LootGoblinCharacterPresentation>();
            SetObjectReference(presentation, "animator", animator);
            SetObjectReference(presentation, "weaponSocket", socket);

            var presentationPrefab = PrefabUtility.SaveAsPrefabAsset(presentationRoot, PresentationPrefabPath);
            if (presentationPrefab == null) throw new InvalidOperationException("Character presentation prefab could not be saved.");

            weaponDefinition = CreateWeaponDefinition(controller, hammerPosition, hammerEuler, hammerScale);
            CreateGroundMaterial();
            CreatePlayerPrefab(presentationPrefab, weaponDefinition, runClip != null);
            CreateProductionScene();
            AddProductionSceneToBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateProductionAssets(runClip != null);

            string finalBuildLabHash = File.Exists(BuildLabScenePath) ? HashFile(BuildLabScenePath) : "missing";
            if (!string.Equals(buildLabHash, finalBuildLabHash, StringComparison.Ordinal))
                throw new InvalidOperationException("BuildLab scene bytes changed during production setup.");

            var report = new StringBuilder();
            report.AppendLine("PASS production foundation assets created");
            report.AppendLine($"PASS production scene: {ProductionScenePath}");
            report.AppendLine($"PASS canonical Player prefab: {PlayerPrefabPath}");
            report.AppendLine($"PASS reusable character presentation prefab: {PresentationPrefabPath}");
            report.AppendLine($"PASS reusable temporary weapon prefab: {HammerPrefabPath}");
            report.AppendLine($"PASS attack clip imported as non-looping: {AttackClipPath}");
            report.AppendLine(runClip == null ? "INFO no valid Run clip was found; controller contains Idle and Walk." : "PASS valid Run clip found and included.");
            report.AppendLine($"PASS BuildLab scene unchanged (SHA-256 {finalBuildLabHash})");
            WriteResult(report.ToString());
        }
        finally
        {
            if (presentationRoot != null) UnityEngine.Object.DestroyImmediate(presentationRoot);
            if (rigInstance != null) UnityEngine.Object.DestroyImmediate(rigInstance);
        }
    }

    static AnimationClip FindClip(string path, string namePart)
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().ToArray();
        return clips.FirstOrDefault(clip => string.Equals(clip.name, namePart, StringComparison.OrdinalIgnoreCase))
            ?? clips.FirstOrDefault(clip => clip.name.EndsWith("preset:biped:" + namePart, StringComparison.OrdinalIgnoreCase))
            ?? clips.FirstOrDefault(clip => clip.name.IndexOf(namePart, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    static AnimationClip FindRunClip()
    {
        foreach (string path in new[] { RigPath, WalkSourcePath, "Assets/Goblin_With_Hair.fbx" })
        {
            var candidate = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(clip => clip.name.IndexOf("run", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                        !clip.legacy && clip.length > .1f);
            if (candidate != null) return candidate;
        }
        return null;
    }

    static AnimationClip CreateClipCopy(AnimationClip source, string path, bool loop)
    {
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing != null) return existing;

        var copy = UnityEngine.Object.Instantiate(source);
        copy.name = Path.GetFileNameWithoutExtension(path);
        var settings = AnimationUtility.GetAnimationClipSettings(copy);
        settings.loopTime = loop;
        settings.loopBlend = loop;
        settings.keepOriginalPositionY = true;
        settings.keepOriginalPositionXZ = true;
        settings.keepOriginalOrientation = true;
        AnimationUtility.SetAnimationClipSettings(copy, settings);
        copy.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
        AssetDatabase.CreateAsset(copy, path);
        return copy;
    }

    static AnimatorController CreateAnimatorController(AnimationClip idle, AnimationClip walk,
        AnimationClip run, AnimationClip attack)
    {
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (existing != null) return existing;

        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

        var machine = controller.layers[0].stateMachine;
        var idleState = machine.AddState("Idle", new Vector3(250f, 100f));
        var walkState = machine.AddState("Walk", new Vector3(250f, 220f));
        idleState.motion = idle;
        walkState.motion = walk;
        idleState.tag = "Locomotion";
        walkState.tag = "Locomotion";
        machine.defaultState = idleState;

        var toWalk = AddTransition(idleState, walkState, .1f, hasExitTime: false);
        toWalk.AddCondition(AnimatorConditionMode.Greater, .1f, "MoveSpeed");
        var toIdle = AddTransition(walkState, idleState, .1f, hasExitTime: false);
        toIdle.AddCondition(AnimatorConditionMode.Less, .1f, "MoveSpeed");

        AnimatorState runState = null;
        if (run != null)
        {
            runState = machine.AddState("Run", new Vector3(250f, 340f));
            runState.motion = run;
            runState.tag = "Locomotion";
            var walkToRun = AddTransition(walkState, runState, .12f, hasExitTime: false);
            walkToRun.AddCondition(AnimatorConditionMode.Greater, .65f, "MoveSpeed");
            var runToWalk = AddTransition(runState, walkState, .12f, hasExitTime: false);
            runToWalk.AddCondition(AnimatorConditionMode.Less, .65f, "MoveSpeed");
        }

        var attackState = machine.AddState("Hammer_Overhead_Smash_Test", new Vector3(550f, 210f));
        attackState.motion = attack;
        attackState.tag = "Attack";
        foreach (var locomotion in runState == null
                     ? new[] { idleState, walkState }
                     : new[] { idleState, walkState, runState })
        {
            var toAttack = AddTransition(locomotion, attackState, .06f, hasExitTime: false);
            toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
        }

        var attackToIdle = AddTransition(attackState, idleState, .06f, hasExitTime: true, exitTime: 1f);
        attackToIdle.AddCondition(AnimatorConditionMode.Less, .1f, "MoveSpeed");
        if (runState == null)
        {
            var attackToWalk = AddTransition(attackState, walkState, .06f, hasExitTime: true, exitTime: 1f);
            attackToWalk.AddCondition(AnimatorConditionMode.Greater, .1f, "MoveSpeed");
        }
        else
        {
            var attackToWalk = AddTransition(attackState, walkState, .06f, hasExitTime: true, exitTime: 1f);
            attackToWalk.AddCondition(AnimatorConditionMode.Greater, .1f, "MoveSpeed");
            attackToWalk.AddCondition(AnimatorConditionMode.Less, .65f, "MoveSpeed");
            var attackToRun = AddTransition(attackState, runState, .06f, hasExitTime: true, exitTime: 1f);
            attackToRun.AddCondition(AnimatorConditionMode.Greater, .65f, "MoveSpeed");
        }

        EditorUtility.SetDirty(controller);
        return controller;
    }

    static AnimatorStateTransition AddTransition(AnimatorState from, AnimatorState to,
        float duration, bool hasExitTime, float exitTime = 0f)
    {
        var transition = from.AddTransition(to);
        transition.hasExitTime = hasExitTime;
        transition.exitTime = exitTime;
        transition.duration = duration;
        transition.hasFixedDuration = true;
        transition.canTransitionToSelf = false;
        transition.interruptionSource = TransitionInterruptionSource.None;
        return transition;
    }

    static void CreateHammerPrefab(GameObject sourceHammer)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(HammerPrefabPath) != null) return;
        GameObject copy = UnityEngine.Object.Instantiate(sourceHammer);
        try
        {
            copy.name = "TemporaryHammer";
            copy.transform.SetParent(null, false);
            copy.transform.localPosition = Vector3.zero;
            copy.transform.localRotation = Quaternion.identity;
            copy.transform.localScale = Vector3.one;
            var renderers = copy.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                throw new InvalidOperationException("The temporary hammer source node contains no renderers.");
            if (PrefabUtility.SaveAsPrefabAsset(copy, HammerPrefabPath) == null)
                throw new InvalidOperationException("Temporary Hammer prefab could not be saved.");
        }
        finally { UnityEngine.Object.DestroyImmediate(copy); }
    }

    static ProductionWeaponDefinition CreateWeaponDefinition(AnimatorController controller,
        Vector3 position, Vector3 euler, Vector3 scale)
    {
        var definition = AssetDatabase.LoadAssetAtPath<ProductionWeaponDefinition>(HammerDefinitionPath);
        if (definition != null) return definition;

        definition = ScriptableObject.CreateInstance<ProductionWeaponDefinition>();
        var serialized = new SerializedObject(definition);
        serialized.FindProperty("weaponType").enumValueIndex = (int)ProductionWeaponType.Hammer;
        serialized.FindProperty("weaponPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(HammerPrefabPath);
        serialized.FindProperty("animatorController").objectReferenceValue = controller;
        serialized.FindProperty("attackStateName").stringValue = "Hammer_Overhead_Smash_Test";
        serialized.FindProperty("socketLocalPosition").vector3Value = position;
        serialized.FindProperty("socketLocalEulerAngles").vector3Value = euler;
        serialized.FindProperty("socketLocalScale").vector3Value = scale;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(definition, HammerDefinitionPath);
        return definition;
    }

    static void CreateGroundMaterial()
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath) != null) return;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) throw new InvalidOperationException("No supported Lit shader was found for the production test floor.");
        var material = new Material(shader) { name = "ProductionGround" };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(.20f, .235f, .27f, 1f));
        else if (material.HasProperty("_Color")) material.SetColor("_Color", new Color(.20f, .235f, .27f, 1f));
        AssetDatabase.CreateAsset(material, GroundMaterialPath);
    }

    static void CreatePlayerPrefab(GameObject presentationPrefab,
        ProductionWeaponDefinition weaponDefinition, bool hasRunClip)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) != null) return;
        var player = new GameObject("Player");
        try
        {
            var characterPresentation = PrefabUtility.InstantiatePrefab(presentationPrefab) as GameObject;
            if (characterPresentation == null) throw new InvalidOperationException("Character presentation prefab could not be instantiated for the Player.");
            characterPresentation.transform.SetParent(player.transform, false);

            var presentation = characterPresentation.GetComponent<LootGoblinCharacterPresentation>();
            if (presentation == null) throw new InvalidOperationException("Player presentation component is missing from its nested prefab.");
            var animator = presentation.Animator;
            var controller = player.AddComponent<CharacterController>();
            controller.height = TargetCharacterHeight;
            controller.radius = .32f;
            controller.center = new Vector3(0f, TargetCharacterHeight * .5f, 0f);
            controller.stepOffset = .28f;
            controller.slopeLimit = 45f;
            controller.skinWidth = .04f;

            var attachment = player.AddComponent<PlayerWeaponAttachment>();
            SetObjectReference(attachment, "presentation", presentation);
            SetObjectReference(attachment, "animator", animator);
            SetObjectReference(attachment, "initialWeapon", weaponDefinition);

            var playerController = player.AddComponent<ProductionPlayerController>();
            SetObjectReference(playerController, "characterController", controller);
            SetObjectReference(playerController, "animator", animator);
            SetObjectReference(playerController, "weaponAttachment", attachment);
            SetObjectReference(playerController, "inputActions",
                AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions"));
            new SerializedObject(playerController).ApplyModifiedPropertiesWithoutUndo();
            var runProperty = new SerializedObject(playerController).FindProperty("runAnimationAvailable");
            runProperty.boolValue = hasRunClip;
            runProperty.serializedObject.ApplyModifiedPropertiesWithoutUndo();

            if (PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath) == null)
                throw new InvalidOperationException("Canonical Player prefab could not be saved.");
        }
        finally { UnityEngine.Object.DestroyImmediate(player); }
    }

    static void CreateProductionScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ProductionScenePath) != null) return;

        Scene previousActiveScene = SceneManager.GetActiveScene();
        bool replaceCurrentScene = Application.isBatchMode ||
            (SceneManager.sceneCount == 1 && previousActiveScene.IsValid() &&
             string.IsNullOrEmpty(previousActiveScene.path) && previousActiveScene.rootCount == 0);
        if (!Application.isBatchMode && previousActiveScene.IsValid() &&
            string.IsNullOrEmpty(previousActiveScene.path) && !replaceCurrentScene)
            throw new InvalidOperationException("Save the current untitled scene before building the production scene; it contains unsaved objects.");

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
            replaceCurrentScene ? NewSceneMode.Single : NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Gameplay Ground";
            floor.transform.position = new Vector3(0f, -.02f, 0f);
            floor.transform.localScale = new Vector3(2f, 1f, 2f);
            floor.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
            SceneManager.MoveGameObjectToScene(floor, scene);

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var player = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
            if (player == null) throw new InvalidOperationException("Canonical Player prefab could not be added to the production scene.");
            player.transform.position = Vector3.zero;
            SceneManager.MoveGameObjectToScene(player, scene);

            var cameraObject = new GameObject("Production Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 7.5f, -7.5f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.12f, .15f, .18f, 1f);
            camera.fieldOfView = 42f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 100f;
            cameraObject.AddComponent<AudioListener>();
            var follow = cameraObject.AddComponent<ProductionCameraFollow>();
            SetObjectReference(follow, "targetCamera", camera);
            SetObjectReference(follow, "target", player.transform);
            follow.SetTarget(player.transform, true);
            SceneManager.MoveGameObjectToScene(cameraObject, scene);

            var lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            SceneManager.MoveGameObjectToScene(lightObject, scene);

            EditorSceneManager.SaveScene(scene, ProductionScenePath);
        }
        finally
        {
            if (!replaceCurrentScene)
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                    SceneManager.SetActiveScene(previousActiveScene);
            }
        }
    }

    static void AddProductionSceneToBuildSettings()
    {
        var result = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(ProductionScenePath, true)
        };
        result.AddRange(EditorBuildSettings.scenes.Where(scene => scene.path != ProductionScenePath));
        EditorBuildSettings.scenes = result.ToArray();
    }

    static void ValidateProductionAssets(bool hasRunClip)
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (player == null) throw new InvalidOperationException("Player prefab validation: missing asset.");
        var animators = player.GetComponentsInChildren<Animator>(true);
        if (animators.Length != 1) throw new InvalidOperationException($"Player prefab contains {animators.Length} Animators; expected exactly one.");
        if (animators[0].avatar == null || !animators[0].avatar.isHuman || !animators[0].avatar.isValid)
            throw new InvalidOperationException("Player Animator does not reference a valid Humanoid Avatar.");

        var socket = FindDescendant(player.transform, "WeaponSocket_R");
        if (socket == null || socket.parent == null || !socket.parent.name.Contains("Hand"))
            throw new InvalidOperationException("Production Player is missing a right-hand WeaponSocket_R.");
        if (FindDescendant(player.transform, "TEMP_Hammer_Overhead_Smash_Runtime") != null)
            throw new InvalidOperationException("The test-only embedded Hammer node remains in the production character prefab.");

        // This corrected FBX exposes CC_Base_Hip as its imported Humanoid skeleton root;
        // there is no separate Transform literally named "Armature" in this source rig.
        int skeletonRootCount = player.GetComponentsInChildren<Transform>(true)
            .Count(transform => string.Equals(transform.name, "CC_Base_Hip", StringComparison.OrdinalIgnoreCase));
        if (skeletonRootCount != 1)
            throw new InvalidOperationException($"Player prefab contains {skeletonRootCount} CC_Base_Hip skeleton roots; expected one.");

        foreach (var renderer in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer.sharedMesh == null || renderer.bones.Length == 0 || renderer.bones.Any(bone => bone == null))
                throw new InvalidOperationException($"Skinned mesh '{renderer.name}' has incomplete bone bindings.");
        }

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) throw new InvalidOperationException("Production Animator Controller is missing.");
        var states = controller.layers[0].stateMachine.states.Select(child => child.state).ToArray();
        bool HasState(string name) => states.Any(state => state.name == name && state.motion != null);
        if (!HasState("Idle") || !HasState("Walk") || !HasState("Hammer_Overhead_Smash_Test"))
            throw new InvalidOperationException("Animator must contain Idle, Walk, and Hammer_Overhead_Smash_Test states with motions.");
        if (hasRunClip && !HasState("Run")) throw new InvalidOperationException("A valid Run clip was found but no Run state was generated.");
        var attackClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AttackClipPath);
        if (attackClip == null || attackClip.isLooping)
            throw new InvalidOperationException("Hammer attack clip is missing or looping.");

        if (AssetDatabase.LoadAssetAtPath<GameObject>(HammerPrefabPath) == null ||
            AssetDatabase.LoadAssetAtPath<ProductionWeaponDefinition>(HammerDefinitionPath) == null)
            throw new InvalidOperationException("Reusable Hammer prefab or weapon definition is missing.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ProductionScenePath) == null)
            throw new InvalidOperationException("Production gameplay scene is missing.");
    }

    static Bounds GetRendererBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.zero);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static Transform FindDescendant(Transform root, string exactName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == exactName) return child;
        return null;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(propertyName);
        if (property == null) throw new InvalidOperationException($"Serialized field '{propertyName}' was not found on {target.GetType().Name}.");
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static string HashFile(string path)
    {
        using (SHA256 sha256 = SHA256.Create())
            return BitConverter.ToString(sha256.ComputeHash(File.ReadAllBytes(path))).Replace("-", string.Empty);
    }
}
