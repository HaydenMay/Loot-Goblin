using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

/// <summary>Focused Play Mode acceptance check for the isolated production foundation scene.</summary>
[InitializeOnLoad]
public static class ProductionFoundationRuntimeValidation
{
    const string ScenePath = "Assets/Production/Scenes/DungeonRun_Production.unity";
    const string AttackClipPath = "Assets/Production/Animation/Hammer_Overhead_Smash_Test.anim";
    const string PendingKey = "LootGoblin.ProductionValidation.Pending";
    const string CompleteKey = "LootGoblin.ProductionValidation.Complete";
    const string ResultKey = "LootGoblin.ProductionValidation.Result";
    const string ExitCodeKey = "LootGoblin.ProductionValidation.ExitCode";
    const string ResultPath = "Logs/ProductionFoundationRuntimeValidation.txt";

    enum Phase { Idle, Move, Stop, Attack, Recover }

    static bool started;
    static bool wasAttackState;
    static bool attackKeyReleased;
    static bool moveSucceeded;
    static bool walkObserved;
    static bool idleObserved;
    static int attackEntries;
    static float maximumAttackTime;
    static float attackClipLength;
    static double phaseStartedAt;
    static double testStartedAt;
    static Phase phase;
    static Keyboard validationKeyboard;
    static ProductionPlayerController playerController;
    static PlayerWeaponAttachment weaponAttachment;
    static LootGoblinCharacterPresentation presentation;
    static ProductionCameraFollow cameraFollow;
    static SkinnedMeshRenderer backpackRenderer;
    static Vector3 playerStartPosition;
    static Vector3 cameraStartPosition;
    static StringBuilder report;

    static ProductionFoundationRuntimeValidation()
    {
        EditorApplication.update += Tick;
    }

    public static void RunAndExit()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SessionState.SetBool(PendingKey, true);
        SessionState.SetBool(CompleteKey, false);
        SessionState.SetString(ResultKey, string.Empty);
        SessionState.SetInt(ExitCodeKey, 1);
        Directory.CreateDirectory("Logs");
        EditorApplication.isPlaying = true;
    }

    static void Tick()
    {
        if (SessionState.GetBool(CompleteKey, false) &&
            !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            File.WriteAllText(ResultPath, SessionState.GetString(ResultKey, "No validation report was produced."));
            int exitCode = SessionState.GetInt(ExitCodeKey, 1);
            SessionState.SetBool(CompleteKey, false);
            EditorApplication.Exit(exitCode);
            return;
        }

        if (!SessionState.GetBool(PendingKey, false) || !EditorApplication.isPlaying) return;

        try
        {
            if (!started) StartValidation();
            if (EditorApplication.timeSinceStartup - testStartedAt > 12d)
                throw new TimeoutException("Production Play Mode validation exceeded 12 seconds.");
            AdvanceValidation();
        }
        catch (Exception exception)
        {
            AddFailure("Runtime validation exception: " + exception.Message);
            CompleteValidation();
        }
    }

    static void StartValidation()
    {
        playerController = UnityEngine.Object.FindAnyObjectByType<ProductionPlayerController>();
        cameraFollow = UnityEngine.Object.FindAnyObjectByType<ProductionCameraFollow>();
        weaponAttachment = UnityEngine.Object.FindAnyObjectByType<PlayerWeaponAttachment>();
        presentation = UnityEngine.Object.FindAnyObjectByType<LootGoblinCharacterPresentation>();
        if (playerController == null || cameraFollow == null || weaponAttachment == null || presentation == null)
            return;
        if (presentation.Animator == null || presentation.WeaponSocket == null)
            throw new InvalidOperationException("Player presentation is missing its Animator or WeaponSocket_R.");

        validationKeyboard = InputSystem.AddDevice<Keyboard>("ProductionValidationKeyboard");
        if (validationKeyboard == null) throw new InvalidOperationException("Could not create a synthetic keyboard for the input check.");
        InputSystem.QueueStateEvent(validationKeyboard, new KeyboardState());

        attackClipLength = AssetDatabase.LoadAssetAtPath<AnimationClip>(AttackClipPath).length;
        report = new StringBuilder();
        testStartedAt = phaseStartedAt = EditorApplication.timeSinceStartup;
        phase = Phase.Idle;
        playerStartPosition = playerController.transform.position;
        cameraStartPosition = cameraFollow.transform.position;
        backpackRenderer = FindBackpackRenderer(playerController.gameObject);
        Application.logMessageReceived += OnLogMessage;
        started = true;
    }

    static void AdvanceValidation()
    {
        var animator = presentation.Animator;
        TrackAttackState(animator);
        ValidateBackpackBounds();
        double elapsed = EditorApplication.timeSinceStartup - phaseStartedAt;

        switch (phase)
        {
            case Phase.Idle:
                if (elapsed < .75d) return;
                idleObserved = IsCurrentOrNext(animator, "Idle");
                if (!idleObserved) AddFailure("Animator did not settle in Idle before movement.");
                report.AppendLine(idleObserved ? "PASS Animator entered Idle." : "FAIL Animator Idle state was not observed.");
                playerStartPosition = playerController.transform.position;
                cameraStartPosition = cameraFollow.transform.position;
                InputSystem.QueueStateEvent(validationKeyboard, new KeyboardState(Key.W));
                phase = Phase.Move;
                phaseStartedAt = EditorApplication.timeSinceStartup;
                return;

            case Phase.Move:
                if (elapsed < .9d) return;
                float moved = Vector3.Distance(playerStartPosition, playerController.transform.position);
                moveSucceeded = moved > .35f;
                if (!moveSucceeded) AddFailure($"Player movement distance was only {moved:F2} units.");
                walkObserved = IsCurrentOrNext(animator, "Walk");
                if (!walkObserved) AddFailure("Animator did not transition to Walk while the Move action was held.");
                float cameraError = Vector3.Distance(cameraFollow.transform.position,
                    playerController.transform.position + new Vector3(0f, 7.5f, -7.5f));
                bool cameraMoved = Vector3.Distance(cameraStartPosition, cameraFollow.transform.position) > .25f;
                if (!cameraMoved || cameraError > .75f)
                    AddFailure($"Camera follow did not settle on the moving player (error {cameraError:F2}).");
                report.AppendLine(moveSucceeded ? $"PASS Move action moved the player {moved:F2} units." : "FAIL Player did not move.");
                report.AppendLine(walkObserved ? "PASS Animator transitioned to Walk." : "FAIL Walk transition was not observed.");
                report.AppendLine(cameraMoved && cameraError <= .75f
                    ? $"PASS Camera followed the player (offset error {cameraError:F2})."
                    : $"FAIL Camera follow error was {cameraError:F2}.");
                InputSystem.QueueStateEvent(validationKeyboard, new KeyboardState());
                phase = Phase.Stop;
                phaseStartedAt = EditorApplication.timeSinceStartup;
                return;

            case Phase.Stop:
                if (elapsed < .5d) return;
                bool idleAfterMove = IsCurrentOrNext(animator, "Idle");
                if (!idleAfterMove) AddFailure("Animator did not return from Walk to Idle after movement stopped.");
                report.AppendLine(idleAfterMove ? "PASS Animator returned from Walk to Idle." : "FAIL Idle return after movement was not observed.");
                InputSystem.QueueStateEvent(validationKeyboard, new KeyboardState(Key.W, Key.Enter));
                phase = Phase.Attack;
                phaseStartedAt = EditorApplication.timeSinceStartup;
                return;

            case Phase.Attack:
                if (!attackKeyReleased && elapsed > .12d)
                {
                    InputSystem.QueueStateEvent(validationKeyboard, new KeyboardState(Key.W));
                    attackKeyReleased = true;
                }
                float attackTestTime = Mathf.Max(1.5f, attackClipLength + .55f);
                if (elapsed < attackTestTime) return;
                string attackName = weaponAttachment.AttackStateName;
                bool returnedToWalk = IsCurrentOrNext(animator, "Walk");
                if (attackEntries != 1)
                    AddFailure($"Hammer attack entered {attackEntries} times; expected exactly once.");
                if (maximumAttackTime < .95f)
                    AddFailure($"Hammer attack reached only normalized time {maximumAttackTime:F2}.");
                if (!returnedToWalk)
                    AddFailure("Hammer attack did not return to locomotion while movement continued.");
                if (Vector3.Distance(playerStartPosition, playerController.transform.position) < .5f)
                    AddFailure("Movement did not continue during the Hammer attack.");
                bool facingForward = Vector3.Dot(playerController.transform.forward, Vector3.forward) > .9f;
                if (!facingForward) AddFailure("Player facing did not remain aligned with the active movement direction.");
                bool hammerAttached = IsWeaponAttachedToSocket();
                if (!hammerAttached) AddFailure("Temporary Hammer was not parented to WeaponSocket_R.");
                report.AppendLine(attackEntries == 1
                    ? $"PASS {attackName} triggered once."
                    : $"FAIL {attackName} entered {attackEntries} times.");
                report.AppendLine(maximumAttackTime >= .95f
                    ? $"PASS Hammer attack reached normalized time {maximumAttackTime:F2} before exit."
                    : $"FAIL Hammer attack reached only {maximumAttackTime:F2}.");
                report.AppendLine(returnedToWalk
                    ? "PASS Hammer attack returned to Walk while movement remained active."
                    : "FAIL Hammer attack did not return to locomotion.");
                report.AppendLine(hammerAttached
                    ? "PASS Temporary Hammer follows the right-hand socket as a child prefab."
                    : "FAIL Temporary Hammer socket attachment failed.");
                report.AppendLine(facingForward ? "PASS Player facing followed movement during the attack." : "FAIL Player facing was lost.");
                InputSystem.QueueStateEvent(validationKeyboard, new KeyboardState());
                phase = Phase.Recover;
                phaseStartedAt = EditorApplication.timeSinceStartup;
                return;

            case Phase.Recover:
                if (elapsed < .4d) return;
                bool idleAfterAttack = IsCurrentOrNext(animator, "Idle");
                if (!idleAfterAttack) AddFailure("Animator did not settle back to Idle after attack and movement ended.");
                report.AppendLine(idleAfterAttack ? "PASS Animator returned to Idle after attack." : "FAIL Animator did not return to Idle after attack.");
                ValidateRigAndBackpack();
                CompleteValidation();
                return;
        }
    }

    static void TrackAttackState(Animator animator)
    {
        string attackName = weaponAttachment.AttackStateName;
        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        bool inAttack = IsState(current, attackName);
        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            inAttack |= IsState(next, attackName);
        }

        if (inAttack && !wasAttackState)
        {
            attackEntries++;
        }
        if (IsState(current, attackName))
            maximumAttackTime = Mathf.Max(maximumAttackTime, current.normalizedTime);
        wasAttackState = inAttack;
    }

    static bool IsCurrentOrNext(Animator animator, string stateName)
    {
        if (IsState(animator.GetCurrentAnimatorStateInfo(0), stateName)) return true;
        return animator.IsInTransition(0) && IsState(animator.GetNextAnimatorStateInfo(0), stateName);
    }

    static bool IsState(AnimatorStateInfo state, string shortName)
    {
        return state.shortNameHash == Animator.StringToHash(shortName) || state.IsName(shortName);
    }

    static bool IsWeaponAttachedToSocket()
    {
        var socket = presentation.WeaponSocket;
        var hammer = weaponAttachment.EquippedInstance;
        return socket != null && hammer != null && hammer.transform.parent == socket &&
               Vector3.Distance(hammer.transform.position, socket.TransformPoint(hammer.transform.localPosition)) < .001f;
    }

    static void ValidateRigAndBackpack()
    {
        var animator = presentation.Animator;
        bool avatarValid = animator != null && animator.avatar != null &&
                           animator.avatar.isValid && animator.avatar.isHuman;
        if (!avatarValid) AddFailure("Runtime Animator does not have one valid Humanoid Avatar.");
        int animatorCount = playerController.GetComponentsInChildren<Animator>(true).Length;
        int skeletonRootCount = playerController.GetComponentsInChildren<Transform>(true)
            .Count(transform => transform.name == "CC_Base_Hip");
        if (animatorCount != 1) AddFailure($"Runtime Player has {animatorCount} Animators; expected one.");
        if (skeletonRootCount != 1) AddFailure($"Runtime Player has {skeletonRootCount} Humanoid skeleton roots; expected one.");

        Bounds visualBounds = GetRendererBounds(playerController.gameObject);
        bool intendedScale = Mathf.Abs(visualBounds.size.y - 1.7f) < .12f;
        if (!intendedScale) AddFailure($"Player visual height was {visualBounds.size.y:F2} units; expected about 1.70.");
        bool backpackStable = backpackRenderer != null && backpackRenderer.rootBone != null &&
                              backpackRenderer.bones.Length > 0 && backpackRenderer.bones.All(bone => bone != null) &&
                              IsFinite(backpackRenderer.bounds) && backpackRenderer.bounds.size.sqrMagnitude > .001f &&
                              backpackRenderer.bounds.size.magnitude < 5f;
        if (!backpackStable) AddFailure("Backpack skinned mesh or its runtime bounds were unstable.");

        report.AppendLine(avatarValid && animatorCount == 1 && skeletonRootCount == 1
            ? "PASS one valid Humanoid Avatar, one Animator, and one CC_Base_Hip skeleton root."
            : "FAIL Humanoid rig or Animator validation.");
        report.AppendLine(intendedScale
            ? $"PASS Goblin visual height is {visualBounds.size.y:F2} units at gameplay scale."
            : $"FAIL Goblin visual height is {visualBounds.size.y:F2} units.");
        report.AppendLine(backpackStable
            ? $"PASS backpack skinned mesh retained {backpackRenderer.bones.Length} valid bones and finite bounds."
            : "FAIL backpack weighting or bounds validation.");
    }

    static SkinnedMeshRenderer FindBackpackRenderer(GameObject player)
    {
        return player.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .FirstOrDefault(renderer => renderer.name.Contains("002") ||
                                        (renderer.sharedMesh != null && renderer.sharedMesh.name.Contains("002")));
    }

    static Bounds GetRendererBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.zero);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static void ValidateBackpackBounds()
    {
        if (backpackRenderer != null && !IsFinite(backpackRenderer.bounds))
            AddFailure("Backpack skinned-mesh bounds became non-finite during animation.");
    }

    static bool IsFinite(Bounds bounds)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        return IsFinite(min) && IsFinite(max);
    }

    static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    static void OnLogMessage(string condition, string stackTrace, LogType type)
    {
        if (type < LogType.Error) return;
        if (condition.IndexOf("avatar", StringComparison.OrdinalIgnoreCase) >= 0 ||
            condition.IndexOf("humanoid", StringComparison.OrdinalIgnoreCase) >= 0 ||
            condition.IndexOf("animator", StringComparison.OrdinalIgnoreCase) >= 0)
            AddFailure("Unity reported an Animator/Avatar error: " + condition);
    }

    static void AddFailure(string message)
    {
        report ??= new StringBuilder();
        report.AppendLine("FAIL " + message);
    }

    static void CompleteValidation()
    {
        Application.logMessageReceived -= OnLogMessage;
        if (validationKeyboard != null && validationKeyboard.added)
            InputSystem.RemoveDevice(validationKeyboard);

        bool failed = report.ToString().IndexOf("FAIL ", StringComparison.Ordinal) >= 0;
        report.AppendLine(failed ? "RESULT FAIL" : "RESULT PASS");
        SessionState.SetString(ResultKey, report.ToString());
        SessionState.SetInt(ExitCodeKey, failed ? 1 : 0);
        SessionState.SetBool(PendingKey, false);
        SessionState.SetBool(CompleteKey, true);
        EditorApplication.isPlaying = false;
    }
}
