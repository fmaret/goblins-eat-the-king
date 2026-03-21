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

    [Header("Vie")]
    [SerializeField] private float maxHp = 100f;
    [SerializeField] private StatBar healthBar;

    private Animator animator;
    private PlayerMovement playerMovement;

    private NetworkVariable<bool> netIsAttacking = new NetworkVariable<bool>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<float> hp = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> netIsDead = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    void Awake()
    {
        animator = GetComponent<Animator>();
        playerMovement = GetComponent<PlayerMovement>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            hp.Value = maxHp;

        hp.OnValueChanged += OnHpChanged;
        netIsDead.OnValueChanged += (oldV, newV) => { if (newV) animator?.SetTrigger("Die"); };
        // register in GameUI header for all clients
        if (GameUI.Instance != null)
            GameUI.Instance.AddPlayerEntry(OwnerClientId, $"Player {OwnerClientId}");
        else
            StartCoroutine(RegisterWithUI());
        UpdateHealthBar();
    }

    public override void OnNetworkDespawn()
    {
        hp.OnValueChanged -= OnHpChanged;
        if (GameUI.Instance != null)
            GameUI.Instance.RemovePlayerEntry(OwnerClientId);
    }

    public bool IsDead => netIsDead.Value;

    void Update()
    {
        animator.SetBool("isAttacking", netIsAttacking.Value);

        if (!IsOwner) return;
        if (!playerMovement.IsAttacking && InputSystem.actions["Attack"].WasPressedThisFrame())
            StartCoroutine(AttackRoutine());
        if (InputSystem.actions["UpgradeChoiceDebug"].WasPressedThisFrame()) {
            UpgradeChoice.Instance.GenerateNewChoices();
            UpgradeChoice.Instance.SetActive(true);
        }
    }

    // Appelé par EnemyController (côté serveur)
    public void TakeDamage(float damage)
    {
        if (!IsServer) return;
        hp.Value = Mathf.Max(0f, hp.Value - damage);
        // play hit animation on clients
        PlayHitClientRpc();
        if (hp.Value <= 0f)
        {
            netIsDead.Value = true;
            DieClientRpc();
        }
    }

    [ClientRpc]
    private void DieClientRpc()
    {
        if (animator != null)
            animator.SetTrigger("Die");
    }

    [ClientRpc]
    private void PlayHitClientRpc()
    {
        if (animator != null)
            animator.SetTrigger("Hit");
    }

    private void OnHpChanged(float oldHp, float newHp)
    {
        UpdateHealthBar();
    }

    private void UpdateHealthBar()
    {
        string text = $"HP : {Mathf.CeilToInt(hp.Value)}/{maxHp}";
        if (GameUI.Instance != null)
        {
            if (IsOwner)
                GameUI.Instance.SetPlayerHealth(hp.Value, maxHp, text);

            GameUI.Instance.SetPlayerEntryHealth(OwnerClientId, hp.Value, maxHp, text);
        }
        else if (healthBar != null)
        {
            // local fallback for scenes without GameUI
            if (IsOwner)
                healthBar.Set(hp.Value, maxHp, text);
        }
    }

    private IEnumerator RegisterWithUI()
    {
        float waited = 0f;
        const float timeout = 5f;
        while (GameUI.Instance == null && waited < timeout)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        if (GameUI.Instance != null)
            GameUI.Instance.AddPlayerEntry(OwnerClientId, $"Player {OwnerClientId}");
    }

    private IEnumerator AttackRoutine()
    {
        playerMovement.IsAttacking = true;
        netIsAttacking.Value = true;

        yield return null;
        while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.5f)
            yield return null;

        Vector2 dir = new Vector2(
            animator.GetFloat("LastInputX"),
            animator.GetFloat("LastInputY")
        ).normalized;

        int layerMask = enemyLayer == 0 ? ~0 : (int)enemyLayer;
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, attackRange, layerMask);
        DrawDebugCircle(transform.position, attackRange, Color.red, 1f);
        Debug.DrawRay(transform.position, dir * attackRange, Color.yellow, 1f);

        foreach (var hit in hits)
        {
            Vector2 toEnemy = ((Vector2)hit.transform.position - (Vector2)transform.position).normalized;
            if (Vector2.Dot(dir, toEnemy) > 0.3f)
            {
                var enemy = hit.GetComponent<EnemyController>();
                if (enemy != null)
                    enemy.TakeDamageServerRpc(attackDamage);
            }
        }

        while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
            yield return null;

        playerMovement.IsAttacking = false;
        netIsAttacking.Value = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }

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