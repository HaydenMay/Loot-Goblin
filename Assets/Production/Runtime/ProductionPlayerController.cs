using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Minimal production movement, facing, locomotion animation, and one-shot attack input.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class ProductionPlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] CharacterController characterController;
    [SerializeField] Animator animator;
    [SerializeField] PlayerWeaponAttachment weaponAttachment;
    [SerializeField] InputActionAsset inputActions;

    [Header("Input Actions")]
    [SerializeField] string playerMapName = "Player";
    [SerializeField] string moveActionName = "Move";
    [SerializeField] string attackActionName = "Attack";
    [SerializeField] string sprintActionName = "Sprint";

    [Header("Movement")]
    [SerializeField, Min(0f)] float walkSpeed = 2.4f;
    [SerializeField, Min(0f)] float runSpeed = 4.1f;
    [SerializeField, Min(0f)] float turnSpeedDegrees = 720f;
    [SerializeField, Min(0f)] float gravity = 20f;
    [SerializeField] bool runAnimationAvailable;

    [Header("Animation")]
    [SerializeField, Min(0f)] float locomotionDampTime = .1f;
    [SerializeField] string moveSpeedParameter = "MoveSpeed";
    [SerializeField] string attackTriggerParameter = "Attack";
    [SerializeField, Min(.25f)] float attackInputFailsafeSeconds = 3f;

    InputAction moveAction;
    InputAction attackAction;
    InputAction sprintAction;
    bool enabledMoveHere;
    bool enabledAttackHere;
    bool enabledSprintHere;
    bool attackLocked;
    bool attackStateObserved;
    float attackRequestedAt;
    float verticalSpeed;
    int moveSpeedHash;
    int attackTriggerHash;

    public CharacterController CharacterController => characterController;
    public Animator Animator => animator;
    public bool RunAnimationAvailable => runAnimationAvailable;

    void Reset()
    {
        characterController = GetComponent<CharacterController>();
        weaponAttachment = GetComponent<PlayerWeaponAttachment>();
        animator = GetComponentInChildren<Animator>();
    }

    void Awake()
    {
        if (characterController == null) characterController = GetComponent<CharacterController>();
        if (weaponAttachment == null) weaponAttachment = GetComponent<PlayerWeaponAttachment>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = false;
        moveSpeedHash = Animator.StringToHash(moveSpeedParameter);
        attackTriggerHash = Animator.StringToHash(attackTriggerParameter);
    }

    void OnEnable()
    {
        BindInputActions();
    }

    void OnDisable()
    {
        UnbindInputActions();
    }

    void Update()
    {
        if (characterController == null) return;

        Vector2 move = moveAction == null ? Vector2.zero : moveAction.ReadValue<Vector2>();
        move = Vector2.ClampMagnitude(move, 1f);
        Vector3 direction = new Vector3(move.x, 0f, move.y);
        bool moving = direction.sqrMagnitude > .0025f;
        bool running = moving && runAnimationAvailable && sprintAction != null && sprintAction.IsPressed();

        if (moving)
        {
            direction.Normalize();
            Quaternion facing = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, facing,
                turnSpeedDegrees * Time.deltaTime);
        }

        if (characterController.isGrounded && verticalSpeed < 0f)
            verticalSpeed = -2f;
        else
            verticalSpeed -= gravity * Time.deltaTime;

        float moveSpeed = running ? runSpeed : walkSpeed;
        Vector3 velocity = (moving ? direction * moveSpeed : Vector3.zero) + Vector3.up * verticalSpeed;
        characterController.Move(velocity * Time.deltaTime);

        if (animator != null)
        {
            float animationSpeed = !moving ? 0f : move.magnitude * (running ? 1f : .5f);
            animator.SetFloat(moveSpeedHash, animationSpeed, locomotionDampTime, Time.deltaTime);
        }

        UpdateAttackLock();
    }

    public bool TryAttack()
    {
        if (attackLocked || animator == null) return false;
        attackLocked = true;
        attackStateObserved = false;
        attackRequestedAt = Time.time;
        animator.ResetTrigger(attackTriggerHash);
        animator.SetTrigger(attackTriggerHash);
        return true;
    }

    void BindInputActions()
    {
        if (inputActions == null) return;

        string mapPath = playerMapName + "/";
        moveAction = inputActions.FindAction(mapPath + moveActionName, false);
        attackAction = inputActions.FindAction(mapPath + attackActionName, false);
        sprintAction = inputActions.FindAction(mapPath + sprintActionName, false);

        enabledMoveHere = EnableIfNeeded(moveAction);
        enabledAttackHere = EnableIfNeeded(attackAction);
        enabledSprintHere = EnableIfNeeded(sprintAction);
        if (attackAction != null) attackAction.performed += OnAttackPerformed;
    }

    void UnbindInputActions()
    {
        if (attackAction != null) attackAction.performed -= OnAttackPerformed;
        if (enabledMoveHere && moveAction != null) moveAction.Disable();
        if (enabledAttackHere && attackAction != null) attackAction.Disable();
        if (enabledSprintHere && sprintAction != null) sprintAction.Disable();

        moveAction = null;
        attackAction = null;
        sprintAction = null;
        enabledMoveHere = enabledAttackHere = enabledSprintHere = false;
    }

    static bool EnableIfNeeded(InputAction action)
    {
        if (action == null || action.enabled) return false;
        action.Enable();
        return true;
    }

    void OnAttackPerformed(InputAction.CallbackContext context)
    {
        TryAttack();
    }

    void UpdateAttackLock()
    {
        if (!attackLocked || animator == null) return;

        int attackStateHash = Animator.StringToHash(
            weaponAttachment == null ? "Hammer_Overhead_Smash_Test" : weaponAttachment.AttackStateName);
        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        bool inAttack = current.shortNameHash == attackStateHash;
        bool enteringAttack = animator.IsInTransition(0) &&
            animator.GetNextAnimatorStateInfo(0).shortNameHash == attackStateHash;

        if (inAttack || enteringAttack)
            attackStateObserved = true;
        else if (attackStateObserved || Time.time - attackRequestedAt > attackInputFailsafeSeconds)
        {
            attackLocked = false;
            attackStateObserved = false;
        }
    }
}
