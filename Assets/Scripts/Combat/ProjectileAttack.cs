using UnityEngine;
using Unity.Netcode;
using System.Collections;
using Goblins.Combat;

/// <summary>
/// Attaque projectile: lance un projectile (caillou, etc) vers le joueur.
/// </summary>
public class ProjectileAttack : MonoBehaviour, IEnemyAttack
{
    [SerializeField] private AttackDefinition definition;
    private float lastAttackTime = -999f;

    public bool CanStart(Transform enemyTransform, Vector2 targetDirection, PlayerController nearestPlayer)
    {
        if (definition == null) return false;
        if (definition.projectilePrefab == null) return false;
        if (nearestPlayer == null) return false;

        float dist = Vector2.Distance(enemyTransform.position, nearestPlayer.transform.position);
        if (dist > definition.attackRange) return false;

        if (Time.time < lastAttackTime + definition.cooldown) return false;

        return true;
    }

    public void StartAttack(Transform enemyTransform, Vector2 targetDirection, PlayerController nearestPlayer)
    {
        lastAttackTime = Time.time;
        StartCoroutine(ProjectileCoroutine(enemyTransform, nearestPlayer.transform));
    }

    public float GetRemainingCooldown()
    {
        return Mathf.Max(0f, lastAttackTime + definition.cooldown - Time.time);
    }

    public string GetAttackName() => "ProjectileAttack";

    private IEnumerator ProjectileCoroutine(Transform source, Transform target)
    {
        yield return new WaitForSeconds(definition.windupTime);

        Vector2 direction = ((Vector2)target.position - (Vector2)source.position).normalized;
        SpawnProjectile(source.position, direction);

        yield return new WaitForSeconds(definition.recoveryTime);
    }

    private void SpawnProjectile(Vector3 startPos, Vector2 direction)
    {
        GameObject projectile = Instantiate(definition.projectilePrefab, startPos, Quaternion.identity);
        ProjectileMover.Attach(projectile, direction, definition.projectileSpeed, definition.projectileLifetime);

        var hitComponent = projectile.GetComponent<ProjectileHitComponent>();
        if (hitComponent == null)
            hitComponent = projectile.AddComponent<ProjectileHitComponent>();

        hitComponent.Initialize(definition.damage, definition.knockbackForce, definition.effects);
    }
}

/// <summary>
/// Déplace le projectile chaque frame — fonctionne avec ou sans Rigidbody2D.
/// </summary>
public class ProjectileMover : MonoBehaviour
{
    private Vector2 direction;
    private float speed;
    private Transform homingTarget; // si non-null : suit la cible en temps réel

    public static void Attach(GameObject proj, Vector2 dir, float spd, float lifetime, Transform target = null)
    {
        var rb = proj.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = dir * spd;
        }

        var mover = proj.AddComponent<ProjectileMover>();
        mover.direction = dir;
        mover.speed = spd;
        mover.homingTarget = target;

        if (lifetime > 0f)
            Destroy(proj, lifetime);
    }

    void Update()
    {
        // Homing : met à jour la direction vers la cible à chaque frame
        if (homingTarget != null)
        {
            direction = ((Vector2)homingTarget.position - (Vector2)transform.position).normalized;
            var rb = GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = direction * speed;
        }

        transform.Translate(direction * speed * Time.deltaTime, Space.World);
    }
}

/// <summary>
/// Composant attaché au projectile pour détecter les hits joueur.
/// </summary>
public class ProjectileHitComponent : MonoBehaviour
{
    private float damage;
    private float knockbackForce;
    private System.Collections.Generic.List<AttackEffect> effects;
    private bool hasHit = false;

    public void Initialize(float dmg, float kb, System.Collections.Generic.List<AttackEffect> eff, float lifetime = 0f)
    {
        damage = dmg;
        knockbackForce = kb;
        effects = eff ?? new System.Collections.Generic.List<AttackEffect>();
        if (lifetime > 0f)
            Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (hasHit) return;

        // Les dégâts ne sont appliqués que côté serveur pour éviter les doublons
        if (Unity.Netcode.NetworkManager.Singleton != null && !Unity.Netcode.NetworkManager.Singleton.IsServer)
        {
            // Client pur : détruire le projectile visuellement au contact du joueur
            var playerVisual = collision.GetComponent<PlayerController>();
            if (playerVisual != null)
            {
                hasHit = true;
                Destroy(gameObject);
            }
            return;
        }

        var player = collision.GetComponent<PlayerController>();
        if (player != null)
        {
            hasHit = true;
            Vector2 knockbackDir = ((Vector2)player.transform.position - (Vector2)transform.position).normalized;
            HitData hitData = new HitData(damage, transform.position, player.transform.position)
                .WithKnockback(knockbackDir, knockbackForce);

            if (effects != null && effects.Count > 0)
            {
                foreach (var effect in effects)
                    hitData = hitData.WithEffect(effect);
            }

            player.ApplyHit(hitData);
            Destroy(gameObject);
        }
    }
}
