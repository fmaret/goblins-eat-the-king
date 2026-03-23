using Unity.Netcode;
using Goblins.Data;
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

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float sprintMultiplier = 2f;

    public float MoveSpeed => moveSpeed;
    public float SprintMultiplier => sprintMultiplier;

    // Getters / setters for stats
    public float AttackDamage { get => attackDamage; set => attackDamage = value; }
    public float MagicAttackDamage { get => magicAttackDamage; set => magicAttackDamage = value; }
    public float Defense { get => defense; set => defense = value; }
    public float MagicDefense { get => magicDefense; set => magicDefense = value; }
    public float LifeSteal { get => lifeSteal; set => lifeSteal = Mathf.Max(0f, value); }
    public float ManaSteal { get => manaSteal; set => manaSteal = Mathf.Max(0f, value); }
    public float EnduranceSteal { get => enduranceSteal; set => enduranceSteal = Mathf.Max(0f, value); }
    public float HpRegeneration { get => hpRegeneration; set => hpRegeneration = Mathf.Max(0f, value); }
    public float MpRegeneration { get => mpRegeneration; set => mpRegeneration = Mathf.Max(0f, value); }
    public float EnduranceRegeneration { get => enduranceRegeneration; set => enduranceRegeneration = Mathf.Max(0f, value); }
    public float AttackRangeStat { get => attackRange; set => attackRange = Mathf.Max(0f, value); }
    public LayerMask EnemyLayer { get => enemyLayer; set => enemyLayer = value; }

    public float MaxHpStat
    {
        get => maxHp;
        set
        {
            maxHp = Mathf.Max(1f, value);
            if (IsServer) hp.Value = Mathf.Min(hp.Value, maxHp);
            UpdateHealthBar();
        }
    }

    public float MaxMpStat
    {
        get => maxMp;
        set
        {
            maxMp = Mathf.Max(0f, value);
            if (IsServer) mp.Value = Mathf.Min(mp.Value, maxMp);
            UpdateManaBar();
        }
    }

    public float MaxEnduranceStat
    {
        get => maxEndurance;
        set
        {
            maxEndurance = Mathf.Max(0f, value);
            if (IsServer) endurance.Value = Mathf.Min(endurance.Value, maxEndurance);
            UpdateEnduranceBar();
        }
    }

    // Expose some stats for UI queries
    public float CurrentHp => hp.Value;
    public float MaxHp => maxHp;
    public float CurrentMp => mp.Value;
    public float MaxMp => maxMp;
    public float CurrentEndurance => endurance.Value;
    public float MaxEndurance => maxEndurance;

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
    public bool HasEndurance => endurance.Value > 0f;

    // generic accessor for UI / other systems
    public float GetStatValue(StatType stat)
    {
        switch (stat)
        {
            case StatType.HP: return CurrentHp;
            case StatType.MP: return CurrentMp;
            case StatType.ENDURANCE: return CurrentEndurance;
            case StatType.HP_REGENERATION: return hpRegeneration;
            case StatType.MP_REGENERATION: return mpRegeneration;
            case StatType.ENDURANCE_REGENERATION: return enduranceRegeneration;
            case StatType.SPEED: return moveSpeed;
            case StatType.ATTACK: return attackDamage;
            case StatType.MAGIC_ATTACK: return magicAttackDamage;
            case StatType.DEFENSE: return defense;
            case StatType.MAGIC_DEFENSE: return magicDefense;
            case StatType.LIFESTEAL: return lifeSteal;
            case StatType.MANASTEAL: return manaSteal;
            case StatType.ENDURANCESTEAL: return enduranceSteal;
            case StatType.RANGE: return attackRange;
            default: return 0f;
        }
    }

    public override void OnNetworkSpawn()
    {
        // Applique les upgrades achetées (local uniquement, avant que le serveur lise maxHp etc.)
        if (IsOwner)
        {
            var upg = StatUpgradeManager.Instance;
            if (upg != null)
            {
                maxHp               += upg.GetMaxHpBonus();
                maxMp               += upg.GetMaxMpBonus();
                maxEndurance        += upg.GetMaxEnduranceBonus();
                attackDamage        += upg.GetAttackDamageBonus();
                magicAttackDamage   += upg.GetMagicAttackBonus();
                defense             += upg.GetDefenseBonus();
                magicDefense        += upg.GetMagicDefenseBonus();
                hpRegeneration      += upg.GetHpRegenBonus();
                mpRegeneration      += upg.GetMpRegenBonus();
                attackRange         += upg.GetAttackRangeBonus();
            }

            if (!IsServer)
                InitUpgradedStatsServerRpc(maxHp, maxMp, maxEndurance,
                    attackDamage, magicAttackDamage, defense, magicDefense,
                    hpRegeneration, mpRegeneration, attackRange);
        }

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
        if (InputSystem.actions["ShowStats"].WasPressedThisFrame()) {
            if (DisplayStats.Instance == null)
            {
                Debug.LogWarning("No DisplayStats instance available");
                return;
            }
            if (DisplayStats.Instance.IsOpen)
            {
                DisplayStats.Instance.SetActive(false);
            }
            else
            {
                DisplayStats.Instance.DisplayPlayerStats(this);
                DisplayStats.Instance.SetActive(true);
            }
        }
         if (InputSystem.actions["Escape"].WasPressedThisFrame()) {
            EscapeMenuManager.Instance.Toggle();
        }
    }

    [ServerRpc(RequireOwnership = true)]
    private void InitUpgradedStatsServerRpc(float mHp, float mMp, float mEnd,
        float atk, float mAtk, float def, float mDef,
        float hpReg, float mpReg, float range)
    {
        maxHp             = mHp;
        maxMp             = mMp;
        maxEndurance      = mEnd;
        attackDamage      = atk;
        magicAttackDamage = mAtk;
        defense           = def;
        magicDefense      = mDef;
        hpRegeneration    = hpReg;
        mpRegeneration    = mpReg;
        attackRange       = range;
        hp.Value          = maxHp;
        mp.Value          = maxMp;
        endurance.Value   = maxEndurance;
    }

    // Request from owning client to apply a powerup (runs on server)
    [ServerRpc(RequireOwnership = true)]
    public void RequestApplyPowerupServerRpc(int statInt, float value, int targetPlayerIndex, bool isUpgrade, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        ApplyPowerupToTargets(statInt, value, targetPlayerIndex, isUpgrade);
    }

    private void ApplyPowerupToTargets(int statInt, float value, int targetPlayerIndex, bool isUpgrade)
    {
        if (NetworkManager.Singleton == null) return;
        var clients = NetworkManager.Singleton.ConnectedClientsList;
        if (clients == null) return;

        void ApplyToClient(Unity.Netcode.NetworkClient client)
        {
            if (client.PlayerObject == null) return;
            var pc = client.PlayerObject.GetComponent<PlayerController>();
            if (pc != null) pc.ApplyPowerup(statInt, value, isUpgrade);
        }

        if (targetPlayerIndex == 0)
        {
            foreach (var c in clients) ApplyToClient(c);
        }
        else
        {
            int idx = targetPlayerIndex - 1;
            if (idx >= 0 && idx < clients.Count) ApplyToClient(clients[idx]);
        }
        // notify affected clients so their local PlayerController instances update for display
        NotifyClientsOfPowerup(statInt, value, targetPlayerIndex, isUpgrade);
        DisplayStats.Instance?.DisplayPlayerStats(this);
    }

    // notify the affected client(s) so their local PlayerController reflects the stat change for UI
    // Called from server
    private void NotifyClientsOfPowerup(int statInt, float value, int targetPlayerIndex, bool isUpgrade)
    {
        if (NetworkManager.Singleton == null) return;
        var clients = NetworkManager.Singleton.ConnectedClientsList;
        if (clients == null) return;

        void NotifyClient(Unity.Netcode.NetworkClient client)
        {
            if (client.PlayerObject == null) return;
            var pc = client.PlayerObject.GetComponent<PlayerController>();
            if (pc == null) return;
            var rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { client.ClientId } }
            };
            pc.ApplyPowerupClientRpc(statInt, value, isUpgrade, rpcParams);
        }

        if (targetPlayerIndex == 0)
        {
            // notify all clients
            foreach (var c in clients) NotifyClient(c);
        }
        else
        {
            int idx = targetPlayerIndex - 1;
            if (idx >= 0 && idx < clients.Count) NotifyClient(clients[idx]);
        }
    }

    // Apply a stat change on this player (server-only authoritative)
    public void ApplyPowerup(int statInt, float value, bool isUpgrade)
    {
        var stat = (StatType)statInt;
        float sign = isUpgrade ? 1f : -1f;
        switch (stat)
        {
            case StatType.HP:
                maxHp = Mathf.Max(1f, maxHp + sign * value);
                if (IsServer) hp.Value = Mathf.Min(hp.Value, maxHp);
                UpdateHealthBar();
                break;
            case StatType.MP:
                maxMp = Mathf.Max(0f, maxMp + sign * value);
                if (IsServer) mp.Value = Mathf.Min(mp.Value, maxMp);
                UpdateManaBar();
                break;
            case StatType.ENDURANCE:
                maxEndurance = Mathf.Max(0f, maxEndurance + sign * value);
                if (IsServer) endurance.Value = Mathf.Min(endurance.Value, maxEndurance);
                UpdateEnduranceBar();
                break;
            case StatType.HP_REGENERATION:
                hpRegeneration = Mathf.Max(0f, hpRegeneration + sign * value);
                break;
            case StatType.MP_REGENERATION:
                mpRegeneration = Mathf.Max(0f, mpRegeneration + sign * value);
                break;
            case StatType.ENDURANCE_REGENERATION:
                enduranceRegeneration = Mathf.Max(0f, enduranceRegeneration + sign * value);
                break;
            case StatType.SPEED:
                // modify PlayerController-held move speed
                moveSpeed = Mathf.Max(0f, moveSpeed + sign * value);
                break;
            case StatType.ATTACK:
                attackDamage += sign * value;
                break;
            case StatType.MAGIC_ATTACK:
                magicAttackDamage += sign * value;
                break;
            case StatType.DEFENSE:
                defense += sign * value;
                break;
            case StatType.MAGIC_DEFENSE:
                magicDefense += sign * value;
                break;
            case StatType.ATTACK_SPEED:
                // not implemented: could modify animator speed or attack timing
                break;
            case StatType.LIFESTEAL:
                lifeSteal = Mathf.Max(0f, lifeSteal + sign * value);
                break;
            case StatType.MANASTEAL:
                manaSteal = Mathf.Max(0f, manaSteal + sign * value);
                break;
            case StatType.ENDURANCESTEAL:
                enduranceSteal = Mathf.Max(0f, enduranceSteal + sign * value);
                break;
            case StatType.RANGE:
                attackRange = Mathf.Max(0f, attackRange + sign * value);
                break;
            default:
                Debug.LogWarning($"ApplyPowerup: stat {stat} not handled on PlayerController");
                break;
        }
        // after applying server-side, notify the owner client so it can update its local display
        if (IsServer)
        {
            // notify only the affected players (owner clients of the target player objects)
            // we don't know targetPlayerIndex here; the caller ApplyPowerupToTargets will call NotifyClientsOfPowerup separately
        }
    }

    [ClientRpc]
    private void ApplyPowerupClientRpc(int statInt, float value, bool isUpgrade, ClientRpcParams clientRpcParams = default)
    {
        // run on client: apply a local-only version so UI and local state reflect the change
        if (IsServer) return;
        ApplyPowerupLocal(statInt, value, isUpgrade);
    }

    // apply stat changes locally (client-side display / local values)
    private void ApplyPowerupLocal(int statInt, float value, bool isUpgrade)
    {
        var stat = (StatType)statInt;
        float sign = isUpgrade ? 1f : -1f;
        switch (stat)
        {
            case StatType.HP:
                maxHp = Mathf.Max(1f, maxHp + sign * value);
                UpdateHealthBar();
                break;
            case StatType.MP:
                maxMp = Mathf.Max(0f, maxMp + sign * value);
                UpdateManaBar();
                break;
            case StatType.ENDURANCE:
                maxEndurance = Mathf.Max(0f, maxEndurance + sign * value);
                UpdateEnduranceBar();
                break;
            case StatType.HP_REGENERATION:
                hpRegeneration = Mathf.Max(0f, hpRegeneration + sign * value);
                break;
            case StatType.MP_REGENERATION:
                mpRegeneration = Mathf.Max(0f, mpRegeneration + sign * value);
                break;
            case StatType.ENDURANCE_REGENERATION:
                enduranceRegeneration = Mathf.Max(0f, enduranceRegeneration + sign * value);
                break;
            case StatType.SPEED:
                moveSpeed = Mathf.Max(0f, moveSpeed + sign * value);
                break;
            case StatType.ATTACK:
                attackDamage += sign * value;
                break;
            case StatType.MAGIC_ATTACK:
                magicAttackDamage += sign * value;
                break;
            case StatType.DEFENSE:
                defense += sign * value;
                break;
            case StatType.MAGIC_DEFENSE:
                magicDefense += sign * value;
                break;
            case StatType.LIFESTEAL:
                lifeSteal = Mathf.Max(0f, lifeSteal + sign * value);
                break;
            case StatType.MANASTEAL:
                manaSteal = Mathf.Max(0f, manaSteal + sign * value);
                break;
            case StatType.ENDURANCESTEAL:
                enduranceSteal = Mathf.Max(0f, enduranceSteal + sign * value);
                break;
            case StatType.RANGE:
                attackRange = Mathf.Max(0f, attackRange + sign * value);
                break;
            default:
                Debug.LogWarning($"ApplyPowerupLocal: stat {stat} not handled on PlayerController");
                break;
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
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySwordSlice();

        yield return null;
        while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.5f)
            yield return null;

        Vector2 dir = new Vector2(
            animator.GetFloat("LastInputX"),
            animator.GetFloat("LastInputY")
        ).normalized;

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
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, attackRange);
        foreach (var hit in hits)
        {
            Vector2 toTarget = ((Vector2)hit.transform.position - (Vector2)transform.position).normalized;
            if (Vector2.Dot(dir, toTarget) > 0.3f)
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
                var pot = hit.GetComponentInParent<PotController>();
                if (pot != null) pot.TakeDamage();

            }
        }
    }

    private float regenAccumulator = 0f;
    void FixedUpdate()
    {
        if (!IsServer) return;
        // apply regeneration every 0.1 second
        regenAccumulator += Time.fixedDeltaTime;
        if (regenAccumulator >= 0.1f)
        {
            regenAccumulator = 0f;
            if (hp.Value > 0f && hp.Value < maxHp)
            {
                hp.Value = Mathf.Min(maxHp, hp.Value + hpRegeneration * 0.1f);
                OnHpChanged(hp.Value - hpRegeneration * 0.1f, hp.Value);
            }
            if (mp.Value < maxMp)
                mp.Value = Mathf.Min(maxMp, mp.Value + mpRegeneration * 0.1f);
            if (endurance.Value < maxEndurance && !playerMovement.IsSprinting)
                endurance.Value = Mathf.Min(maxEndurance, endurance.Value + enduranceRegeneration * 0.1f);
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

    public string GetStats()
    {
        return $"ATK: {attackDamage:0.##}\n" +
               $"MATK: {magicAttackDamage:0.##}\n" +
               $"DEF: {defense:0.##}\n" +
               $"MDEF: {magicDefense:0.##}\n" +
               $"LIFESTEAL: {lifeSteal:P0}\n" +
               $"MANASTEAL: {manaSteal:P0}\n" +
               $"ENDSTEAL: {enduranceSteal:P0}\n" +
               $"HP Regen: {hpRegeneration:0.##}/s\n" +
               $"MP Regen: {mpRegeneration:0.##}/s\n" +
               $"END Regen: {enduranceRegeneration:0.##}/s\n" +
               $"MOVE SPD: {moveSpeed:0.##}\n" +
               $"SPRINT: x{sprintMultiplier:0.##}";
    }
}