using Unity.Netcode;
using Unity.Collections;
using Goblins.Data;
using Goblins.Combat;
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
    [SerializeField] private float criticalRate = 0.05f;       // probabilité 0-1
    [SerializeField] private float criticalDamage = 1.5f;    // multiplicateur de dégâts
    [SerializeField] private float dodgeRate = 0f;           // probabilité 0-1
    [SerializeField] private float lifeSteal = 0f;
    [SerializeField] private float manaSteal = 0f;
    [SerializeField] private float enduranceSteal = 0f;
    [SerializeField] private float hpRegeneration = 0.01f;
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

    [Header("Attaque mêlée")]
    [Tooltip("AttackDefinition ScriptableObject pour l'attaque mêlée de base (barre espace).\nSi non assignée, l'ancien système flat est utilisé.")]
    [SerializeField] private AttackDefinition meleeAttackDefinition;

    public float MoveSpeed { get => moveSpeed; set => moveSpeed = Mathf.Max(0f, value); }
    public float SprintMultiplier => sprintMultiplier;

    // Getters / setters for stats
    public float AttackDamage { get => attackDamage; set => attackDamage = value; }
    public float MagicAttackDamage { get => magicAttackDamage; set => magicAttackDamage = value; }
    public float Defense { get => defense; set => defense = value; }
    public float MagicDefense { get => magicDefense; set => magicDefense = value; }
    public float CriticalRate   { get => criticalRate;   set => criticalRate   = Mathf.Clamp01(value); }
    public float CriticalDamage { get => criticalDamage; set => criticalDamage = Mathf.Max(1f, value); }
    public float DodgeRate      { get => dodgeRate;      set => dodgeRate      = Mathf.Clamp01(value); }
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

    /// <summary>Pseudo du joueur, écrit par l'Owner au spawn, synchronisé sur tous les clients.</summary>
    private NetworkVariable<FixedString64Bytes> netPlayerName = new NetworkVariable<FixedString64Bytes>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

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
            case StatType.CRITICAL_RATE: return criticalRate;
            case StatType.CRITICAL_DAMAGE: return criticalDamage;
            case StatType.DODGE_RATE: return dodgeRate;
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
        netPlayerName.OnValueChanged += OnPlayerNameChanged;

        // L'owner définit son pseudo dès le spawn (synchronisé vers tous les clients via NetworkVariable)
        if (IsOwner)
            netPlayerName.Value = new FixedString64Bytes(GetDisplayName());

        if (GameUI.Instance != null)
            RegisterInGameUI();
        else
            StartCoroutine(RegisterWithUI());
    }

    public override void OnNetworkDespawn()
    {
        hp.OnValueChanged -= OnHpChanged;
        mp.OnValueChanged -= OnMpChanged;
        endurance.OnValueChanged -= OnEnduranceChanged;
        netPlayerName.OnValueChanged -= OnPlayerNameChanged;
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

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
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
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void RequestApplyPowerupServerRpc(int statInt, float value, int targetPlayerIndex, bool isUpgrade)
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
            case StatType.CRITICAL_RATE:
                criticalRate = Mathf.Clamp01(criticalRate + sign * value);
                break;
            case StatType.CRITICAL_DAMAGE:
                criticalDamage = Mathf.Max(1f, criticalDamage + sign * value);
                break;
            case StatType.DODGE_RATE:
                dodgeRate = Mathf.Clamp01(dodgeRate + sign * value);
                break;
            case StatType.RANGE:
                attackRange = Mathf.Max(0f, attackRange + sign * value);
                break;
            default:
                Debug.LogWarning($"ApplyPowerup: stat {stat} not handled on PlayerController");
                break;
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
            case StatType.CRITICAL_RATE:
                criticalRate = Mathf.Clamp01(criticalRate + sign * value);
                break;
            case StatType.CRITICAL_DAMAGE:
                criticalDamage = Mathf.Max(1f, criticalDamage + sign * value);
                break;
            case StatType.DODGE_RATE:
                dodgeRate = Mathf.Clamp01(dodgeRate + sign * value);
                break;
            case StatType.RANGE:
                attackRange = Mathf.Max(0f, attackRange + sign * value);
                break;
            default:
                Debug.LogWarning($"ApplyPowerupLocal: stat {stat} not handled on PlayerController");
                break;
        }
    }

    // Appelé par EnemyController (côté serveur) - backward compat
    public void TakeDamage(float damage)
    {
        if (!IsServer) return;
        HitData hitData = new HitData(damage, transform.position, transform.position);
        ApplyHit(hitData);
    }

    /// <summary>
    /// Applique un hit complet avec effets (dégâts, knockback, stun, etc).
    /// À préférer à TakeDamage() qui n'applique que des dégâts.
    /// </summary>
    public void ApplyHit(HitData hitData)
    {
        if (!IsServer) return;

        // Dodge check
        if (dodgeRate > 0f && Random.value < dodgeRate) return;

        // Apply defense reduction (min 0 — ne peut pas soigner le joueur)
        float effective = Mathf.Max(0f, hitData.damage - defense);
        hp.Value = Mathf.Max(0f, hp.Value - effective);

        // Play hit animation on clients
        PlayHitClientRpc();

        // Apply knockback if present
        if (hitData.knockbackForce > 0f && playerMovement != null)
        {
            Vector2 knockbackDir = hitData.knockbackDirection.normalized;
            if (knockbackDir == Vector2.zero)
                knockbackDir = ((Vector2)transform.position - hitData.sourcePosition).normalized;

            playerMovement.ApplyKnockback(knockbackDir * hitData.knockbackForce);
        }

        // Apply effects (stun, slow, bleed, poison, etc)
        if (hitData.effects != null && hitData.effects.Count > 0)
        {
            foreach (var effect in hitData.effects)
            {
                ApplyEffect(effect);
            }
        }

        if (hp.Value <= 0f)
        {
            netIsDead.Value = true;
            DieClientRpc();
        }
    }

    private void ApplyEffect(AttackEffect effect)
    {
        switch (effect.effectType)
        {
            case AttackEffectType.Damage:
                // Already applied above
                break;
            case AttackEffectType.Knockback:
                // Already applied above
                break;
            case AttackEffectType.Slow:
                if (playerMovement != null)
                    StartCoroutine(ApplySlowCoroutine(effect.value, effect.duration));
                break;
            case AttackEffectType.Stun:
                if (playerMovement != null)
                    StartCoroutine(ApplyStunCoroutine(effect.duration));
                break;
            case AttackEffectType.Bleed:
                // DoT: 50% damage per tick
                StartCoroutine(ApplyDoTCoroutine(effect.value, effect.duration, 0.5f));
                break;
            case AttackEffectType.Poison:
                // DoT: 30% damage per tick
                StartCoroutine(ApplyDoTCoroutine(effect.value, effect.duration, 0.3f));
                break;
        }
    }

    private IEnumerator ApplySlowCoroutine(float slowPercent, float duration)
    {
        float originalSpeed = moveSpeed;
        moveSpeed *= (1f - Mathf.Clamp01(slowPercent));
        if (playerMovement != null)
            playerMovement.moveSpeed = moveSpeed;

        yield return new WaitForSeconds(duration);

        moveSpeed = originalSpeed;
        if (playerMovement != null)
            playerMovement.moveSpeed = moveSpeed;
    }

    private IEnumerator ApplyStunCoroutine(float duration)
    {
        if (playerMovement == null) yield break;

        bool wasControlled = playerMovement.enabled;
        playerMovement.enabled = false;

        yield return new WaitForSeconds(duration);

        playerMovement.enabled = wasControlled;
    }

    private IEnumerator ApplyDoTCoroutine(float baseDamage, float duration, float percentPerTick)
    {
        float elapsed = 0f;
        float tickInterval = 0.1f;

        while (elapsed < duration && IsServer && !netIsDead.Value)
        {
            yield return new WaitForSeconds(tickInterval);
            elapsed += tickInterval;

            float tickDamage = baseDamage * percentPerTick;
            float effective = Mathf.Max(1f, tickDamage - defense * 0.5f); // defense réduit DoT
            hp.Value = Mathf.Max(0f, hp.Value - effective);
            PlayHitClientRpc();

            if (hp.Value <= 0f)
            {
                netIsDead.Value = true;
                DieClientRpc();
                break;
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

    private void OnPlayerNameChanged(FixedString64Bytes oldName, FixedString64Bytes newName)
    {
        if (GameUI.Instance != null)
            GameUI.Instance.RenamePlayerEntry(OwnerClientId, newName.ToString());
    }

    /// <summary>Retourne le pseudo à afficher : LocalPlayerName pour l'owner, netPlayerName pour les autres.</summary>
    private string GetDisplayName()
    {
        if (IsOwner)
        {
            string name = Goblins.Lobby.LobbyManager.LocalPlayerName;
            return string.IsNullOrWhiteSpace(name) ? $"Player {OwnerClientId}" : name;
        }
        return netPlayerName.Value.IsEmpty ? $"Player {OwnerClientId}" : netPlayerName.Value.ToString();
    }

    /// <summary>Crée l'entrée joueur dans le GameUI et pousse les valeurs de stats initiales.</summary>
    private void RegisterInGameUI()
    {
        if (GameUI.Instance == null) return;
        string name = GetDisplayName();
        GameUI.Instance.AddPlayerEntry(OwnerClientId, name);
        GameUI.Instance.RenamePlayerEntry(OwnerClientId, name);
        UpdateHealthBar();
        UpdateManaBar();
        UpdateEnduranceBar();
    }

    private IEnumerator RegisterWithUI()
    {
        // L'objet joueur NGO persiste à travers les transitions de scène (pas de despawn/respawn).
        // On attend que GameUI soit disponible (chargement de la scène de jeu) sans timeout fixe.
        // On s'arrête seulement si l'objet est réellement dépawn (fin de session réseau).
        while (GameUI.Instance == null && IsSpawned)
            yield return null;

        if (!IsSpawned || GameUI.Instance == null) yield break;

        // Pour l'owner : re-lire LocalPlayerName sauvegardé avant le chargement de scène,
        // pour récupérer le pseudo définitif tapé dans le lobby après le spawn initial.
        if (IsOwner)
        {
            string finalName = GetDisplayName();
            if (netPlayerName.Value.ToString() != finalName)
                netPlayerName.Value = new FixedString64Bytes(finalName);
        }
        else if (netPlayerName.Value.IsEmpty)
        {
            // Attendre un frame si la NetworkVariable n'est pas encore synchronisée (rare)
            yield return null;
        }

        RegisterInGameUI();
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
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void AttackServerRpc(Vector2 dir)
    {
        if (!IsServer) return;

        float range    = meleeAttackDefinition != null ? meleeAttackDefinition.areaRadius : attackRange;
        float baseDmg  = meleeAttackDefinition != null ? meleeAttackDefinition.damage      : attackDamage;
        float kbForce  = meleeAttackDefinition != null ? meleeAttackDefinition.knockbackForce : 0f;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, range);
        foreach (var hit in hits)
        {
            Vector2 toTarget = ((Vector2)hit.transform.position - (Vector2)transform.position).normalized;
            if (Vector2.Dot(dir, toTarget) <= 0.3f) continue;

            var enemy = hit.GetComponent<EnemyController>();
            if (enemy != null)
            {
                bool isCrit = criticalRate > 0f && Random.value < criticalRate;
                float damageDealt = isCrit ? baseDmg * criticalDamage : baseDmg;

                if (meleeAttackDefinition != null)
                {
                    // Système data-driven : HitData avec knockback + effets
                    Vector2 kbDir = toTarget;
                    var hitData = new HitData(damageDealt, transform.position, hit.transform.position)
                        .WithKnockback(kbDir, kbForce);
                    if (meleeAttackDefinition.effects != null)
                        foreach (var fx in meleeAttackDefinition.effects)
                            hitData = hitData.WithEffect(fx);
                    enemy.ApplyHit(hitData);
                }
                else
                {
                    enemy.ApplyDamage(damageDealt);
                }

                // Steal (inchangé)
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

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void ConsumeEnduranceServerRpc(float amount, RpcParams rpcParams = default)
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
               $"CRIT: {criticalRate:P0} x{criticalDamage:0.##}\n" +
               $"DODGE: {dodgeRate:P0}\n" +
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