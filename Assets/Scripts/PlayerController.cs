using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerController : NetworkBehaviour
{
    [Header("Stats")]
    [SerializeField] private float attackDamage = 25f;
    [SerializeField] private float magicAttackDamage = 15f;
    [SerializeField] private float defense = 5f;
    [SerializeField] private float magicDefense = 3f;
    [SerializeField] private float lifeSteal = 0f;
    [SerializeField] private float manaSteal = 0f;
    [SerializeField] private float enduranceSteal = 0f;
    [SerializeField] private float hpRegeneration = 1f;
    [SerializeField] private float mpRegeneration = 2f;
    [SerializeField] private float enduranceRegeneration = 5f;
    [SerializeField] private float attackRange = 0.8f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float maxHp = 100f;
    [SerializeField] private float maxMp = 50f;
    [SerializeField] private float maxEndurance = 100f;

    [SerializeField] private StatBar healthBar;

    private Animator animator;
    private PlayerMovement playerMovement;

    private NetworkVariable<bool> netIsAttacking = new NetworkVariable<bool>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<float> hp = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<float> mp = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<float> endurance = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> netIsDead = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    void Awake()
    {
        animator = GetComponent<Animator>();
        playerMovement = GetComponent<PlayerMovement>();
    }

    // expose endurance to other components (read-only)
    public float CurrentEndurance => endurance.Value;
    public bool HasEndurance => endurance.Value > 0f;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            hp.Value = maxHp;
            mp.Value = maxMp;
            endurance.Value = maxEndurance;
            if (DungeonGenerator.Instance != null)
                transform.position = DungeonGenerator.Instance.SpawnPosition;
        }

        hp.OnValueChanged += OnHpChanged;
        mp.OnValueChanged += OnMpChanged;
        endurance.OnValueChanged += OnEnduranceChanged;
        netIsDead.OnValueChanged += (oldV, newV) => { if (newV) animator?.SetTrigger("Die"); };
        // register in GameUI header for all clients
        if (GameUI.Instance != null)
            GameUI.Instance.AddPlayerEntry(OwnerClientId, $"Player {OwnerClientId}");
        else
            StartCoroutine(RegisterWithUI());
        UpdateHealthBar();
        UpdateManaBar();
        UpdateEnduranceBar();
    }

    public override void OnNetworkDespawn()
    {
        hp.OnValueChanged -= OnHpChanged;
        mp.OnValueChanged -= OnMpChanged;
        endurance.OnValueChanged -= OnEnduranceChanged;
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
        // apply defense reduction (minimum 1 damage)
        float effective = Mathf.Max(1f, damage - defense);
        hp.Value = Mathf.Max(0f, hp.Value - effective);
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

    private void OnMpChanged(float oldMp, float newMp)
    {
        UpdateManaBar();
    }

    private void OnEnduranceChanged(float oldE, float newE)
    {
        UpdateEnduranceBar();
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

    private void UpdateManaBar()
    {
        string text = $"MP : {Mathf.CeilToInt(mp.Value)}/{maxMp}";
        if (GameUI.Instance != null)
        {
            if (IsOwner)
                GameUI.Instance.SetPlayerMana(mp.Value, maxMp, text);

            GameUI.Instance.SetPlayerEntryMana(OwnerClientId, mp.Value, maxMp, text);
        }
    }

    private void UpdateEnduranceBar()
    {
        string text = $"END : {Mathf.CeilToInt(endurance.Value)}/{maxEndurance}";
        if (GameUI.Instance != null)
        {
            if (IsOwner)
                GameUI.Instance.SetPlayerEndurance(endurance.Value, maxEndurance, text);

            GameUI.Instance.SetPlayerEntryEndurance(OwnerClientId, endurance.Value, maxEndurance, text);
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

        // request server to perform the attack logic so damage/steal/regeneration are authoritative
        AttackServerRpc(dir);

        while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
            yield return null;

        playerMovement.IsAttacking = false;
        netIsAttacking.Value = false;
    }

    // Server-side attack handling: perform overlap and apply damage + steal
    [ServerRpc(RequireOwnership = false)]
    public void AttackServerRpc(Vector2 dir, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        int layerMask = enemyLayer == 0 ? ~0 : (int)enemyLayer;
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, attackRange, layerMask);
        foreach (var hit in hits)
        {
            Vector2 toEnemy = ((Vector2)hit.transform.position - (Vector2)transform.position).normalized;
            if (Vector2.Dot(dir, toEnemy) > 0.3f)
            {
                var enemy = hit.GetComponent<EnemyController>();
                if (enemy != null)
                {
                    // apply damage on enemy server-side
                    float damageDealt = attackDamage; // could be extended with crits, modifiers, etc.
                    enemy.ApplyDamage(damageDealt);

                    // apply life/mana/endurance steal to this player
                    if (lifeSteal > 0f)
                    {
                        float heal = damageDealt * lifeSteal;
                        hp.Value = Mathf.Min(maxHp, hp.Value + heal);
                        OnHpChanged(hp.Value - heal, hp.Value);
                    }
                    if (manaSteal > 0f)
                    {
                        float gain = damageDealt * manaSteal;
                        mp.Value = Mathf.Min(maxMp, mp.Value + gain);
                    }
                    if (enduranceSteal > 0f)
                    {
                        float egain = damageDealt * enduranceSteal;
                        endurance.Value = Mathf.Min(maxEndurance, endurance.Value + egain);
                    }
                }
            }
        }
    }

    private float regenAccumulator = 0f;
    void FixedUpdate()
    {
        if (!IsServer) return;
        // apply regeneration every second
        regenAccumulator += Time.fixedDeltaTime;
        if (regenAccumulator >= 1f)
        {
            regenAccumulator = 0f;
            if (hp.Value > 0f && hp.Value < maxHp)
            {
                hp.Value = Mathf.Min(maxHp, hp.Value + hpRegeneration);
                OnHpChanged(hp.Value - hpRegeneration, hp.Value);
            }
            if (mp.Value < maxMp)
                mp.Value = Mathf.Min(maxMp, mp.Value + mpRegeneration);
            if (endurance.Value < maxEndurance)
                endurance.Value = Mathf.Min(maxEndurance, endurance.Value + enduranceRegeneration);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void ConsumeEnduranceServerRpc(float amount, ServerRpcParams rpcParams = default)
    {
        // ensure the RPC caller owns this player
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        ConsumeEnduranceLocal(amount);
    }

    public void RequestConsumeEndurance(float amount)
    {
        if (!IsOwner) return;
        if (IsServer)
        {
            ConsumeEnduranceLocal(amount);
        }
        else
        {
            ConsumeEnduranceServerRpc(amount);
        }
    }

    private void ConsumeEnduranceLocal(float amount)
    {
        endurance.Value = Mathf.Max(0f, endurance.Value - amount);
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