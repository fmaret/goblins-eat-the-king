using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerController : NetworkBehaviour
{
    [Header("Attaque")]
    [SerializeField] private float attackDamage = 25f;
    [SerializeField] private float attackRange = 0.8f;
    [SerializeField] private LayerMask enemyLayer;

    private Animator animator;
    private PlayerMovement playerMovement;

    private NetworkVariable<bool> netIsAttacking = new NetworkVariable<bool>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    void Awake()
    {
        animator = GetComponent<Animator>();
        playerMovement = GetComponent<PlayerMovement>();
    }

    void Update()
    {
        animator.SetBool("isAttacking", netIsAttacking.Value);

        if (!IsOwner) return;
        if (!playerMovement.IsAttacking && InputSystem.actions["Attack"].WasPressedThisFrame())
            StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        playerMovement.IsAttacking = true;
        netIsAttacking.Value = true;

        // Attendre le milieu de l'animation (moment où l'épée "touche")
        yield return null;
        float halfDuration = 0f;
        while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.5f)
        {
            halfDuration += Time.deltaTime;
            yield return null;
        }

        // Overlap au moment de l'impact, dans la direction du joueur
        Vector2 dir = new Vector2(
            animator.GetFloat("LastInputX"),
            animator.GetFloat("LastInputY")
        ).normalized;

        // Diagnostic large : rayon 3f, tous les layers, pour vérifier qu'un collider existe
        int layerMask = enemyLayer == 0 ? ~0 : (int)enemyLayer;

        // Cherche tous les ennemis dans le rayon d'attaque depuis la position du joueur,
        // puis garde uniquement ceux qui sont dans la bonne direction
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, attackRange, layerMask);

        // Dessine le cercle de détection visible en Scene view ET Game view (pendant 1 seconde)
        DrawDebugCircle(transform.position, attackRange, Color.red, 1f);
        // Dessine la direction d'attaque
        Debug.DrawRay(transform.position, dir * attackRange, Color.yellow, 1f);

        Debug.Log($"[PlayerController] Ennemis dans le rayon: {hits.Length}, direction: {dir}");
        foreach (var hit in hits)
        {
            // Vérifie que l'ennemi est globalement dans la direction visée (produit scalaire > 0)
            Vector2 toEnemy = ((Vector2)hit.transform.position - (Vector2)transform.position).normalized;
            float dot = Vector2.Dot(dir, toEnemy);
            Debug.Log($"[PlayerController] {hit.name} | dot={dot:F2}");
            if (dot > 0.3f)
            {
                var enemy = hit.GetComponent<EnemyController>();
                if (enemy != null)
                    enemy.TakeDamageServerRpc(attackDamage);
            }
        }

        // Attendre la fin de l'animation
        while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
            yield return null;

        playerMovement.IsAttacking = false;
        netIsAttacking.Value = false;
    }

    // Visualisation de la zone d'attaque dans l'éditeur
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }

    // Dessine un cercle avec Debug.DrawLine (visible en Scene ET Game view)
    private void DrawDebugCircle(Vector2 center, float radius, Color color, float duration, int segments = 24)
    {
        float angleStep = 360f / segments;
        for (int i = 0; i < segments; i++)
        {
            float a1 = Mathf.Deg2Rad * (i * angleStep);
            float a2 = Mathf.Deg2Rad * ((i + 1) * angleStep);
            Vector2 p1 = center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius;
            Vector2 p2 = center + new Vector2(Mathf.Cos(a2), Mathf.Sin(a2)) * radius;
            Debug.DrawLine(p1, p2, color, duration);
        }
    }
}