using UnityEngine;
using Unity.Netcode;
using Goblins.Combat;
using System.Collections;
using System.Collections.Generic;

public class EnemyController : NetworkBehaviour
{
    [Header("Stats")]
    [SerializeField] private float maxHp = 100f;
    [SerializeField] private StatBar healthBar;

    [Header("Loot")]
    [SerializeField] private GameObject coinPrefab;
    [HideInInspector] public bool isBoss = false;

    [Header("Déplacement")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float detectionRange = 20f;
    [SerializeField] private float blockIntensity = 0.5f; // variation side-step when chasing
    [SerializeField] private float despawnDelay = 5f;

    [Header("Attaque")]
    [SerializeField] private List<AttackDefinition> attackDefinitions = new List<AttackDefinition>();

    // Legacy (backward compat)
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
    public void SetStats(float hp, float damage) { maxHp = hp; attackDamage = damage; }

    public void SetRandomStats(float hpMult, float speedMult, float damageMult, float scaleMult)
    {
        maxHp         *= hpMult;
        attackDamage  *= damageMult;
        moveSpeed     *= speedMult;
        if (enemyMovement != null) enemyMovement.moveSpeed = moveSpeed;
        transform.localScale = Vector3.one * scaleMult;
    }

    private EnemyMovement enemyMovement;
    private Animator animator;
    private EnemyAttackBrain attackBrain;
    private Transform target;
    private IEnemyAttack currentAttack;
    private float seed;
    private float speedMultiplier = 1f;

    void Awake()
    {
        enemyMovement = GetComponent<EnemyMovement>();
        enemyMovement.moveSpeed = moveSpeed;
        animator = GetComponent<Animator>();
        attackBrain = GetComponent<EnemyAttackBrain>();
        seed = Random.value * 1000f;
        speedMultiplier = 0.9f + Random.value * 0.2f; // slight speed variation per enemy

        // Fallback: si pas d'AttackBrain, en crée un et assigne les attaques
        if (attackBrain == null)
            attackBrain = gameObject.AddComponent<EnemyAttackBrain>();

        if (attackBrain != null)
        {
            if (attackDefinitions.Count > 0)
            {
                attackBrain.SetAvailableAttacks(attackDefinitions);
            }
            else
            {
                // Crée une attaque mêlée par défaut
                AttackDefinition defaultAttack = CreateDefaultMeleeAttack();
                attackBrain.SetAvailableAttacks(new List<AttackDefinition> { defaultAttack });
            }
        }
    }

    private AttackDefinition CreateDefaultMeleeAttack()
    {
        AttackDefinition def = ScriptableObject.CreateInstance<AttackDefinition>();
        def.attackType = AttackType.Cone;
        def.damage = attackDamage;
        def.cooldown = attackCooldown;
        def.attackRange = attackRange;
        def.areaRadius = 0.8f;
        def.coneAngle = 90f;
        def.windupTime = 0.2f;
        def.recoveryTime = 0.1f;
        def.knockbackForce = 2f;
        return def;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            hp.Value = maxHp;

        // Ignore les collisions physiques avec les joueurs (évite de les pousser)
        var myCol = GetComponent<Collider2D>();
        if (myCol != null)
        {
            foreach (var player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                var pc = player.GetComponent<Collider2D>();
                if (pc != null) Physics2D.IgnoreCollision(myCol, pc, true);
            }
        }

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
        if (target == null) { enemyMovement.netMovement.Value = Vector2.zero; return; }

        float dist = Vector2.Distance(transform.position, target.position);
        Vector2 toTarget = ((Vector2)target.position - (Vector2)transform.position).normalized;

        // Décide si on chasse ou on reste en place
        // On tient compte de la portée max des attaques (ex: AuraPulse à grande portée)
        float effectiveDetectionRange = Mathf.Max(detectionRange, GetMaxTriggerRange());
        if (dist > effectiveDetectionRange)
        {
            enemyMovement.netMovement.Value = Vector2.zero;
            return;
        }

        // Essaye de lancer une attaque
        if (attackBrain != null && currentAttack == null)
        {
            var nearestPlayer = target.GetComponent<PlayerController>();
            if (nearestPlayer == null)
                nearestPlayer = target.GetComponentInParent<PlayerController>();

            IEnemyAttack attack = attackBrain.SelectAttack(transform, toTarget, nearestPlayer);
            if (attack != null)
            {
                currentAttack = attack;
                enemyMovement.netMovement.Value = Vector2.zero;
                attack.StartAttack(transform, toTarget, nearestPlayer);
                StartCoroutine(WaitForAttackCompletion());
                return;
            }
        }

        // Pas d'attaque possible, on chasse
        // Stopping distance = plus petite attackRange parmi les attaques disponibles
        float stoppingDist = attackBrain != null ? GetMinAttackRange() : attackRange;
        if (dist <= stoppingDist * 0.85f)
        {
            enemyMovement.netMovement.Value = Vector2.zero;
            return;
        }

        Vector2 perp = new Vector2(-toTarget.y, toTarget.x);
        float jitter = Mathf.Sin(Time.time * 2f + seed) * blockIntensity;
        Vector2 dir = (toTarget + perp * jitter).normalized;
        enemyMovement.netMovement.Value = dir * speedMultiplier;
    }

    private IEnumerator WaitForAttackCompletion()
    {
        // Attend la vraie fin de la coroutine d'attaque dans le brain
        yield return new WaitUntil(() => attackBrain == null || !attackBrain.IsExecutingAttack);
        currentAttack = null;
    }

    private float GetMinAttackRange()
    {
        if (attackDefinitions == null || attackDefinitions.Count == 0)
            return attackRange;
        float min = float.MaxValue;
        foreach (var def in attackDefinitions)
            if (def != null && def.attackRange < min) min = def.attackRange;
        return min == float.MaxValue ? attackRange : min;
    }

    private float GetMaxTriggerRange()
    {
        if (attackDefinitions == null || attackDefinitions.Count == 0)
            return detectionRange;
        float max = detectionRange;
        foreach (var def in attackDefinitions)
        {
            if (def == null) continue;
            // AuraPulse : areaRadius = trigger range
            // RandomSpikes : attackRange peut être grand (lancer depuis loin)
            // Autres : attackRange normal
            float triggerRange = def.attackType == AttackType.AuraPulse ? def.areaRadius : def.attackRange;
            if (triggerRange > max) max = triggerRange;
        }
        return max;
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
        ApplyDamage(damage);
    }

    // Apply damage on server (callable from server-side code)
    public void ApplyDamage(float damage)
    {
        if (!IsServer) return;
        if (netIsDead.Value) return;
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
                // Loot pièces
                int coinCount = isBoss ? Random.Range(10, 20) : WeightedCoinDrop();
                if (coinPrefab != null && coinCount > 0)
                    DropCoinsClientRpc(coinCount, isBoss ? 2.5f : 0.5f);

                if (DungeonGenerator.Instance != null)
                    DungeonGenerator.Instance.NotifyEnemyDied(roomX, roomY, transform.position);
                StartCoroutine(DespawnAfterDelay());
            }
        }
    }

    // 60% → 0, 30% → 1, 10% → 2
    private static int WeightedCoinDrop()
    {
        float r = Random.value;
        if (r < 0.60f) return 0;
        if (r < 0.90f) return 1;
        return 2;
    }

    [ClientRpc]
    private void DropCoinsClientRpc(int count, float radius)
    {
        if (coinPrefab == null) return;
        for (int i = 0; i < count; i++)
        {
            Vector2 offset = Random.insideUnitCircle * radius;
            Instantiate(coinPrefab, transform.position + (Vector3)offset, Quaternion.identity);
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
