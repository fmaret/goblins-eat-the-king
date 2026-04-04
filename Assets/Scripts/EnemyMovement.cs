using UnityEngine;
using Unity.Netcode;

// Calqué sur PlayerMovement — gère le déplacement et les animations de l'ennemi.
// EnemyController écrit dans netMovement et netIsAttacking, tous les clients lisent et animent.
public class EnemyMovement : NetworkBehaviour
{
    [HideInInspector] public float moveSpeed = 2f;

    private Rigidbody2D rb;
    private Animator animator;

    // Serveur écrit, tous les clients lisent
    public NetworkVariable<Vector2> netMovement = new NetworkVariable<Vector2>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> netIsAttacking = new NetworkVariable<bool>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> netAttackAnimSpeed = new NetworkVariable<float>(
        1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Accessible par EnemyController pour bloquer le mouvement
    public bool IsAttacking { get; set; }

    // ── Knockback (serveur uniquement) ─────────────────────────────────
    private Vector2 knockbackVelocity;
    private float knockbackDuration;
    private float knockbackElapsed;

    /// <summary>Applique un knockback à l'ennemi (appelé côté serveur).</summary>
    public void ApplyKnockback(Vector2 force)
    {
        knockbackVelocity = force;
        knockbackDuration = 0.15f;
        knockbackElapsed  = 0f;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        // Mise à jour des animations sur tous les clients
        Vector2 movement = netMovement.Value;
        bool isMoving = movement != Vector2.zero;

        animator.SetBool("isMoving", isMoving);
        animator.SetBool("isAttacking", netIsAttacking.Value);
        animator.speed = netIsAttacking.Value ? netAttackAnimSpeed.Value : 1f;

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
        if (!IsServer) return;

        if (knockbackElapsed < knockbackDuration)
        {
            knockbackElapsed += Time.fixedDeltaTime;
            rb.MovePosition(rb.position + knockbackVelocity * Time.fixedDeltaTime);
            return;
        }

        if (IsAttacking) return;
        rb.MovePosition(rb.position + netMovement.Value * moveSpeed * Time.fixedDeltaTime);
    }
}
