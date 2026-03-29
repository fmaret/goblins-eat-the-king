using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PlayerMovement : NetworkBehaviour
{
    // movement values are stored in PlayerController to keep player stats centralized
    [Header("Stamina")]
    public float enduranceDrainPerSecond = 15f;
    private Rigidbody2D rb;
    private Animator animator;
    private PlayerController playerController;

    // Synchronisées sur le réseau — owner écrit, tous les clients lisent
    private NetworkVariable<Vector2> netMovement = new NetworkVariable<Vector2>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool> netIsSprinting = new NetworkVariable<bool>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    // Knockback
    private Vector2 knockbackVelocity = Vector2.zero;
    private float knockbackDuration = 0f;
    private float knockbackElapsed = 0f;

    // Accessible par PlayerController pour bloquer le mouvement pendant l'attaque
    public bool IsAttacking { get; set; }
    public bool IsSprinting => netIsSprinting.Value;

    // Public accessor for movement speed (used by effects, etc)
    public float moveSpeed
    {
        get => playerController != null ? playerController.MoveSpeed : 3f;
        set { if (playerController != null) playerController.MoveSpeed = value; }
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        playerController = GetComponent<PlayerController>();
    }

    void Update()
    {
        if (IsOwner)
        {
            // Ne pas lire l'input si le joueur est mort ou si l'escape menu est ouvert
            if ((playerController != null && playerController.IsDead) || (EscapeMenuManager.Instance != null && EscapeMenuManager.Instance.IsOpen) || (UpgradeChoice.Instance != null && UpgradeChoice.Instance.gameObject.activeSelf))
            {
                netMovement.Value = Vector2.zero;
                netIsSprinting.Value = false;
            }
            else
            {
                netMovement.Value = InputSystem.actions["Move"].ReadValue<Vector2>();
                bool wantSprint = InputSystem.actions["Sprint"].IsPressed();
                // prevent sprint if no endurance
                if (wantSprint && playerController != null && playerController.HasEndurance == false)
                    netIsSprinting.Value = false;
                else
                    netIsSprinting.Value = wantSprint;
            }
        }

        Vector2 movement = netMovement.Value;
        bool isMoving = movement != Vector2.zero;

        // si mort, forcer l'animation idle
        if (playerController != null && playerController.IsDead)
            isMoving = false;

        animator.SetBool("isMoving", isMoving);
        animator.SetBool("isSprinting", netIsSprinting.Value && isMoving);

        if (isMoving)
        {
            Vector2 dir = movement.normalized;
            animator.SetFloat("InputX", dir.x);
            animator.SetFloat("InputY", dir.y);
            animator.SetFloat("LastInputX", dir.x);
            animator.SetFloat("LastInputY", dir.y);
        }
    }

    void FixedUpdate()
    {
        if (!IsOwner) return;
        if (EscapeMenuManager.Instance != null && EscapeMenuManager.Instance.IsOpen) return;

        // Apply knockback if active
        if (knockbackElapsed < knockbackDuration)
        {
            float progress = knockbackElapsed / knockbackDuration;
            float decayFactor = 1f - (progress * progress); // Quadratic decay
            rb.linearVelocity = knockbackVelocity * decayFactor;
            knockbackElapsed += Time.fixedDeltaTime;
            return; // Skip normal movement during knockback
        }

        // Normal movement
        if (IsAttacking) return;
        float baseSpeed = 3f;
        float mult = 2f;
        if (playerController != null)
        {
            baseSpeed = playerController.MoveSpeed;
            mult = playerController.SprintMultiplier;
        }
        float currentSpeed = baseSpeed * (netIsSprinting.Value ? mult : 1f);
        rb.MovePosition(rb.position + netMovement.Value * currentSpeed * Time.fixedDeltaTime);

        // consume endurance while sprinting
        if (netIsSprinting.Value && netMovement.Value != Vector2.zero && playerController != null && IsOwner)
        {
            float consume = enduranceDrainPerSecond * Time.fixedDeltaTime;
            playerController.RequestConsumeEndurance(consume);
            // if endurance drained to zero, stop sprinting locally
            if (playerController.HasEndurance == false)
                netIsSprinting.Value = false;
        }
    }

    /// <summary>
    /// Applique un knockback au joueur (appelé côté serveur depuis PlayerController.ApplyHit).
    /// </summary>
    public void ApplyKnockback(Vector2 force)
    {
        knockbackVelocity = force;
        knockbackDuration = 0.2f; // 200ms de knockback
        knockbackElapsed = 0f;
    }
}
