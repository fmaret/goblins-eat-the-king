using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Goblins.Combat;

/// <summary>
/// Cerveau d'attaque entièrement data-driven.
/// Il suffit d'ajouter des AttackDefinitions dans la liste — aucun composant séparé requis.
/// </summary>
public class EnemyAttackBrain : MonoBehaviour
{
    [SerializeField] private List<AttackDefinition> availableAttacks = new List<AttackDefinition>();
    [SerializeField] private bool debugLog = false;

    private Dictionary<AttackDefinition, float> lastAttackTimes = new Dictionary<AttackDefinition, float>();
    private Animator animator;
    private EnemyMovement enemyMovement;

    /// <summary>Vrai pendant toute la durée de l'exécution d'une attaque (windup + hit + recovery).</summary>
    public bool IsExecutingAttack { get; private set; }

    void Awake()
    {
        animator = GetComponent<Animator>();
        enemyMovement = GetComponent<EnemyMovement>();
        foreach (var def in availableAttacks)
            if (def != null) lastAttackTimes[def] = -999f;
    }

    /// <summary>
    /// Retourne la meilleure attaque à lancer maintenant, ou null si aucune disponible.
    /// </summary>
    public IEnemyAttack SelectAttack(Transform enemyTransform, Vector2 targetDirection, PlayerController nearestPlayer)
    {
        if (nearestPlayer == null || availableAttacks.Count == 0)
            return null;

        float dist = Vector2.Distance(enemyTransform.position, nearestPlayer.transform.position);
        if (debugLog)
            Debug.Log($"[EnemyAttackBrain] Evaluating {availableAttacks.Count} attacks, distance: {dist:F2}");

        var candidates = new List<AttackDefinition>();

        foreach (var def in availableAttacks)
        {
            if (def == null) continue;

            float last = lastAttackTimes.TryGetValue(def, out var lt) ? lt : -999f;
            float cdRemaining = Mathf.Max(0f, last + def.cooldown - Time.time);
            // Pour AuraPulse : le déclenchement se fait dès que le joueur entre dans areaRadius
            float triggerRange = def.attackType == AttackType.AuraPulse ? def.areaRadius : def.attackRange;
            bool inRange = dist <= triggerRange;
            bool cdReady  = cdRemaining <= 0f;
            bool prefabOk = def.attackType != AttackType.Projectile || def.projectilePrefab != null;

            if (debugLog)
                Debug.Log($"[EnemyAttackBrain] '{def.name}' ({def.attackType}) | dist={dist:F2}/triggerRange={triggerRange:F2} inRange={inRange} | cd={cdRemaining:F2}s ready={cdReady} | prefabOk={prefabOk}");

            if (!inRange || !cdReady || !prefabOk) continue;
            candidates.Add(def);
        }

        Debug.Log($"[EnemyAttackBrain] {candidates.Count}/{availableAttacks.Count} candidate(s) at dist={dist:F2}");

        if (candidates.Count == 0)
            return null;

        // Sélection pondérée par le champ weight
        float totalWeight = 0f;
        foreach (var c in candidates) totalWeight += Mathf.Max(0.001f, c.weight);
        float roll = Random.value * totalWeight;
        AttackDefinition chosen = candidates[candidates.Count - 1];
        foreach (var c in candidates)
        {
            roll -= Mathf.Max(0.001f, c.weight);
            if (roll <= 0f) { chosen = c; break; }
        }

        Debug.Log($"[EnemyAttackBrain] >>> Launching '{chosen.name}' ({chosen.attackType}) [weight={chosen.weight}/{totalWeight:F2}]");

        return new AttackRunner(this, chosen);
    }

    public void SetAvailableAttacks(List<AttackDefinition> attacks)
    {
        availableAttacks = attacks ?? new List<AttackDefinition>();
        lastAttackTimes.Clear();
        foreach (var def in availableAttacks)
            if (def != null) lastAttackTimes[def] = -999f;
    }

    public int GetAttackCount() => availableAttacks.Count;

    // ── Exécuteur interne ──────────────────────────────────────────────────

    private class AttackRunner : IEnemyAttack
    {
        private readonly EnemyAttackBrain brain;
        private readonly AttackDefinition def;

        public AttackRunner(EnemyAttackBrain brain, AttackDefinition def)
        {
            this.brain = brain;
            this.def = def;
        }

        public bool CanStart(Transform enemyTransform, Vector2 dir, PlayerController nearestPlayer)
        {
            if (nearestPlayer == null) return false;
            float dist = Vector2.Distance(enemyTransform.position, nearestPlayer.transform.position);
            float triggerRange = def.attackType == AttackType.AuraPulse ? def.areaRadius : def.attackRange;
            if (dist > triggerRange) return false;
            float last = brain.lastAttackTimes.TryGetValue(def, out var t) ? t : -999f;
            if (Time.time < last + def.cooldown) return false;
            if (def.attackType == AttackType.Projectile && def.projectilePrefab == null) return false;
            return true;
        }

        public void StartAttack(Transform enemyTransform, Vector2 dir, PlayerController nearestPlayer)
        {
            brain.lastAttackTimes[def] = Time.time;
            brain.IsExecutingAttack = true;
            Debug.Log($"[EnemyAttackBrain] StartAttack: '{def.name}' ({def.attackType})");
            brain.StartCoroutine(Execute(enemyTransform, dir, nearestPlayer));
        }

        public float GetRemainingCooldown()
        {
            float last = brain.lastAttackTimes.TryGetValue(def, out var t) ? t : -999f;
            return Mathf.Max(0f, last + def.cooldown - Time.time);
        }

        public string GetAttackName() => def != null ? $"{def.name} ({def.attackType})" : "Unknown";

        // ── Dispatch ──────────────────────────────────────────────────────

        private IEnumerator Execute(Transform source, Vector2 dir, PlayerController nearestPlayer)
        {
            // Attendre un frame pour que l'animator passe Walking → Idle avant de déclencher Attacking
            yield return null;

            if (brain.enemyMovement != null)
            {
                float totalDuration = def.windupTime + def.recoveryTime;
                brain.enemyMovement.netAttackAnimSpeed.Value = totalDuration > 0f
                    ? def.animationClipDuration / totalDuration
                    : 1f;
                brain.enemyMovement.netIsAttacking.Value = true;
            }
            // Setter directement sur le serveur pour éviter le décalage d'une frame
            if (brain.animator != null)
            {
                brain.animator.SetBool("isAttacking", true);
                brain.animator.speed = def.animationClipDuration / Mathf.Max(0.01f, def.windupTime + def.recoveryTime);
            }

            Debug.Log($"[EnemyAttackBrain] Executing '{def.name}' ({def.attackType})");

            switch (def.attackType)
            {
                case AttackType.Melee:        yield return brain.StartCoroutine(ExecuteMelee(source));                       break;
                case AttackType.Cone:         yield return brain.StartCoroutine(ExecuteCone(source, dir));                   break;
                case AttackType.AuraPulse:    yield return brain.StartCoroutine(ExecuteAuraPulse(source));                   break;
                case AttackType.RandomSpikes: yield return brain.StartCoroutine(ExecuteRandomSpikes(source, nearestPlayer)); break;
                case AttackType.Projectile:   yield return brain.StartCoroutine(ExecuteProjectile(source, dir, nearestPlayer));   break;
            }

            Debug.Log($"[EnemyAttackBrain] Finished '{def.name}' ({def.attackType})");

            // Stop animation immédiatement après le hit
            if (brain.enemyMovement != null)
                brain.enemyMovement.netIsAttacking.Value = false;
            if (brain.animator != null)
            {
                brain.animator.SetBool("isAttacking", false);
                brain.animator.speed = 1f;
            }

            // Recovery : l'ennemi peut bouger mais ne peut pas relancer une attaque
            if (def.recoveryTime > 0f)
                yield return new WaitForSeconds(def.recoveryTime);

            brain.IsExecutingAttack = false;
        }

        // ── Melee ─────────────────────────────────────────────────────────

        private IEnumerator ExecuteMelee(Transform source)
        {
            yield return new WaitForSeconds(def.windupTime);
            HitInRadius(source.position, source.position, def.areaRadius, def.knockbackForce);
        }

        // ── Cone ──────────────────────────────────────────────────────────

        private IEnumerator ExecuteCone(Transform source, Vector2 dir)
        {
            yield return new WaitForSeconds(def.windupTime);

            Collider2D[] hits = Physics2D.OverlapCircleAll(source.position, def.areaRadius);
            foreach (var hit in hits)
            {
                var player = hit.GetComponent<PlayerController>();
                if (player == null) continue;
                Vector2 toPlayer = ((Vector2)hit.transform.position - (Vector2)source.position).normalized;
                if (Vector2.Angle(dir, toPlayer) <= def.coneAngle / 2f)
                    ApplyHitToPlayer(player, source.position, hit.transform.position, toPlayer, def.knockbackForce);
            }
        }

        // ── AuraPulse ─────────────────────────────────────────────────────

        private IEnumerator ExecuteAuraPulse(Transform source)
        {
            // Spawne l'effet visuel en enfant de l'ennemi
            GameObject auraFx = null;
            if (def.auraPrefab != null)
                auraFx = Object.Instantiate(def.auraPrefab, source.position, Quaternion.identity, source);

            float elapsed = 0f;
            float lastPulse = -999f;

            while (elapsed < def.duration && source != null)
            {
                if (Time.time >= lastPulse + def.auraPulseInterval)
                {
                    lastPulse = Time.time;
                    HitInRadius(source.position, source.position, def.areaRadius, def.knockbackForce * 0.5f);
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Détruit l'effet visuel à la fin
            if (auraFx != null) Object.Destroy(auraFx);
        }

        // ── RandomSpikes ──────────────────────────────────────────────────

        private IEnumerator ExecuteRandomSpikes(Transform source, PlayerController target)
        {
            if (target == null) { Debug.LogWarning("[Spikes] target is null!"); yield break; }

            Debug.Log($"[Spikes] Starting — count={def.spikeCount} areaRadius={def.areaRadius} spikeHitRadius={def.spikeHitRadius} windupTime={def.windupTime}");

            // Génère les positions des piques autour du joueur
            Vector2[] positions = new Vector2[def.spikeCount];
            for (int i = 0; i < def.spikeCount; i++)
                positions[i] = (Vector2)target.transform.position + Random.insideUnitCircle * def.areaRadius;

            // Télégraphe : spawne les indicateurs visuels
            GameObject[] indicators = new GameObject[def.spikeCount];
            if (def.spikePrefab != null)
                for (int i = 0; i < def.spikeCount; i++)
                    indicators[i] = Object.Instantiate(def.spikePrefab, positions[i], Quaternion.identity);
            else
                Debug.Log("[Spikes] No spikePrefab set (visual only, hit still applies)");

            // Phase windup
            yield return new WaitForSeconds(def.windupTime);

            // Frappe
            float hitRadius = def.spikeHitRadius > 0f ? def.spikeHitRadius : Mathf.Max(0.5f, def.areaRadius * 0.4f);
            Debug.Log($"[Spikes] Hitting {def.spikeCount} positions with hitRadius={hitRadius}");

            for (int i = 0; i < def.spikeCount; i++)
            {
                Debug.Log($"[Spikes] Spike {i} at {positions[i]}");
                Collider2D[] hits = Physics2D.OverlapCircleAll(positions[i], hitRadius);
                Debug.Log($"[Spikes] Spike {i} hit {hits.Length} colliders");
                foreach (var hit in hits)
                    Debug.Log($"[Spikes]   → {hit.gameObject.name} (tag={hit.tag})");

                HitInRadius(source.position, positions[i], hitRadius, def.knockbackForce);
                if (indicators[i] != null) Object.Destroy(indicators[i]);
            }

            Debug.Log("[Spikes] Done");
        }

        // ── Projectile ────────────────────────────────────────────────────

        private IEnumerator ExecuteProjectile(Transform source, Vector2 facingDir, PlayerController target)
        {
            yield return new WaitForSeconds(def.windupTime);

            if (def.projectilePrefab != null)
            {
                Vector2 shootDir;
                Transform homingTarget = null;
                switch (def.projectilePattern)
                {
                    case Goblins.Combat.ProjectilePattern.Straight:
                        shootDir = facingDir;
                        break;
                    case Goblins.Combat.ProjectilePattern.Targeted:
                    default:
                        shootDir = target != null
                            ? ((Vector2)target.transform.position - (Vector2)source.position).normalized
                            : facingDir;
                        homingTarget = target != null ? target.transform : null;
                        break;
                }

                GameObject proj = Object.Instantiate(def.projectilePrefab, source.position, Quaternion.identity);

                ProjectileMover.Attach(proj, shootDir, def.projectileSpeed, def.projectileLifetime, homingTarget);

                var hitComp = proj.GetComponent<ProjectileHitComponent>() ?? proj.AddComponent<ProjectileHitComponent>();
                hitComp.Initialize(def.damage, def.knockbackForce, def.effects, def.projectileLifetime);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private void HitInRadius(Vector2 sourcePos, Vector2 center, float radius, float knockbackForce)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
            foreach (var hit in hits)
            {
                var player = hit.GetComponent<PlayerController>();
                if (player == null) continue;
                Vector2 kb = ((Vector2)hit.transform.position - sourcePos).normalized;
                ApplyHitToPlayer(player, sourcePos, hit.transform.position, kb, knockbackForce);
            }
        }

        private void ApplyHitToPlayer(PlayerController player, Vector2 sourcePos, Vector2 hitPos, Vector2 knockbackDir, float knockbackForce)
        {
            HitData hitData = new HitData(def.damage, sourcePos, hitPos)
                .WithKnockback(knockbackDir, knockbackForce);

            if (def.effects != null && def.effects.Count > 0)
                foreach (var effect in def.effects)
                    hitData = hitData.WithEffect(effect);

            player.ApplyHit(hitData);
        }
    }
}
