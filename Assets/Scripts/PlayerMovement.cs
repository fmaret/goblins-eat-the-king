using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PlayerMovement : NetworkBehaviour
{
    public float moveSpeed = 3f;
    public float sprintMultiplier = 2f;
    private Rigidbody2D rb;
    private Animator animator;

    // Synchronisées sur le réseau — owner écrit, tous les clients lisent
    private NetworkVariable<Vector2> netMovement = new NetworkVariable<Vector2>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool> netIsSprinting = new NetworkVariable<bool>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    // Accessible par PlayerController pour bloquer le mouvement pendant l'attaque
    public bool IsAttacking { get; set; }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log("PlayerMovement spawned for " + OwnerClientId);
    }

    void Update()
    {
        if (IsOwner)
        {
            netMovement.Value = InputSystem.actions["Move"].ReadValue<Vector2>();
            netIsSprinting.Value = InputSystem.actions["Sprint"].IsPressed();
        }

        Vector2 movement = netMovement.Value;
        bool isMoving = movement != Vector2.zero;

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
        if (!IsOwner || IsAttacking) return;
        float currentSpeed = moveSpeed * (netIsSprinting.Value ? sprintMultiplier : 1f);
        rb.MovePosition(rb.position + netMovement.Value * currentSpeed * Time.fixedDeltaTime);
    }
}
