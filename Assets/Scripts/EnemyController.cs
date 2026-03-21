using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class EnemyController : NetworkBehaviour
{
    [Header("Stats")]
    [SerializeField] private float maxHp = 100f;
    [SerializeField] private StatBar healthBar;

    [Header("Déplacement")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float detectionRange = 5f;
    [SerializeField] private float blockIntensity = 0.5f; // variation side-step when chasing
    [SerializeField] private float despawnDelay = 5f;

    [Header("Attaque")]
    [SerializeField] private float attackRange = 0.8f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private LayerMask playerLayer;

    private NetworkVariable<float> hp = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<bool> netIsDead = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [HideInInspector] public int roomX = -1, roomY = -1;
    public void SetRoom(int x, int y) { roomX = x; roomY = y; }

    private EnemyMovement enemyMovement;
    private Animator animator;
    private Transform target;
    private float lastAttackTime = -999f;
    private float seed;
    private float speedMultiplier = 1f;

    private enum State { Idle, Chase, Attack }
    private State state = State.Idle;

    void Awake()
    {
        enemyMovement = GetComponent<EnemyMovement>();
        enemyMovement.moveSpeed = moveSpeed;
        animator = GetComponent<Animator>();
        seed = Random.value * 1000f;
        speedMultiplier = 0.9f + Random.value * 0.2f; // slight speed variation per enemy
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            hp.Value = maxHp;

        hp.OnValueChanged += OnHpChanged;
        netIsDead.OnValueChanged += (oldV, newV) => { if (newV && animator != null) animator.SetTrigger("Die"); };
        UpdateHealthBar();
    }

    public override void OnNetworkDespawn()
    {
        hp.OnValueChanged -= OnHpChanged;
    }

    void Update()
    {
        if (!IsServer) return;

        if (netIsDead.Value) // if dead, ensure no AI updates
        {
            if (enemyMovement != null)
            {
                enemyMovement.netMovement.Value = Vector2.zero;
                enemyMovement.IsAttacking = false;
                enemyMovement.netIsAttacking.Value = false;
            }
            return;
        }

        FindClosestPlayer();
        UpdateState();
    }

    private void FindClosestPlayer()
    {
        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        float closest = float.MaxValue;
        target = null;

        foreach (var player in players)
        {
            float dist = Vector2.Distance(transform.position, player.transform.position);
            if (dist < closest)
            {
                closest = dist;
                target = player.transform;
            }
        }
    }

    private void UpdateState()
    {
        if (target == null) { state = State.Idle; return; }

        float dist = Vector2.Distance(transform.position, target.position);

        if (dist <= attackRange)
            state = State.Attack;
        else if (dist <= detectionRange)
            state = State.Chase;
        else
            state = State.Idle;

        if (state == State.Chase)
        {
            Vector2 toTarget = ((Vector2)target.position - (Vector2)transform.position).normalized;
            // lateral jitter to try to block or flank the player
            Vector2 perp = new Vector2(-toTarget.y, toTarget.x);
            float jitter = Mathf.Sin(Time.time * 2f + seed) * blockIntensity;
            Vector2 dir = (toTarget + perp * jitter).normalized;
            enemyMovement.netMovement.Value = dir * speedMultiplier;
        }
        else
        {
            enemyMovement.netMovement.Value = Vector2.zero;
        }

        if (state == State.Attack && !enemyMovement.IsAttacking && Time.time >= lastAttackTime + attackCooldown)
            StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        enemyMovement.IsAttacking = true;
        enemyMovement.netIsAttacking.Value = true;
        enemyMovement.netMovement.Value = Vector2.zero;
        lastAttackTime = Time.time;

        yield return null;
        while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.5f)
            yield return null;

        // Inflige les dégâts au joueur dans la direction visée
        Vector2 dir = new Vector2(
            animator.GetFloat("LastInputX"),
            animator.GetFloat("LastInputY")
        ).normalized;

        int layerMask = playerLayer == 0 ? ~0 : (int)playerLayer;
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, attackRange, layerMask);
        foreach (var hit in hits)
        {
            Vector2 toTarget = ((Vector2)hit.transform.position - (Vector2)transform.position).normalized;
            if (Vector2.Dot(dir, toTarget) > 0.3f)
            {
                var player = hit.GetComponent<PlayerController>();
                if (player != null)
                    player.TakeDamage(attackDamage);
            }
        }

        while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
            yield return null;

        enemyMovement.IsAttacking = false;
        enemyMovement.netIsAttacking.Value = false;
    }

    private IEnumerator DespawnAfterDelay()
    {
        yield return new WaitForSeconds(despawnDelay);
        if (IsServer)
            GetComponent<NetworkObject>().Despawn();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void TakeDamageServerRpc(float damage)
    {
        hp.Value = Mathf.Max(0f, hp.Value - damage);
        // play hit animation on clients
        PlayHitClientRpc();

        if (hp.Value <= 0f)
        {
            // mark dead and stop movement
            netIsDead.Value = true;
            if (enemyMovement != null)
            {
                enemyMovement.netMovement.Value = Vector2.zero;
                enemyMovement.IsAttacking = false;
                enemyMovement.netIsAttacking.Value = false;
            }

            // schedule despawn on server
            if (IsServer)
            {
                DungeonGenerator.Instance?.NotifyEnemyDied(roomX, roomY);
                StartCoroutine(DespawnAfterDelay());
            }
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

    public void DestroyEnemy()
    {
        if (IsServer)
            GetComponent<NetworkObject>().Despawn();
    }

    private void OnHpChanged(float oldHp, float newHp) => UpdateHealthBar();

    private void UpdateHealthBar()
    {
        if (healthBar != null)
            healthBar.Set(hp.Value, maxHp, $"HP : {Mathf.CeilToInt(hp.Value)}/{maxHp}");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
