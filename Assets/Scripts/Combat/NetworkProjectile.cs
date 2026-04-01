using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using Goblins.Combat;

/// <summary>
/// NetworkBehaviour à placer sur le prefab de projectile (ex: Fireball).
/// Le serveur le spawne via NetworkObject.Spawn() — NGO le réplique automatiquement
/// sur tous les clients. Remplace ProjectileMover + ProjectileHitComponent pour
/// les projectiles réseau.
///
/// ► Ajouter ce script ET un NetworkObject sur le prefab.
/// ► Enregistrer le prefab dans la NetworkPrefabsList du projet.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NetworkProjectile : NetworkBehaviour
{
    /// <summary>Vélocité du projectile (direction × vitesse) — encodée en un seul NetworkVariable
    /// et incluse dans le message de spawn initial reçu par tous les clients.</summary>
    public NetworkVariable<Vector2> netVelocity = new NetworkVariable<Vector2>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Données de hit — serveur uniquement, pas de sync réseau nécessaire
    private float damage;
    private float knockbackForce;
    private List<AttackEffect> effects;
    private float lifetime;
    private bool hasHit;
    private Rigidbody2D rb;

    /// <summary>
    /// Appelé sur le serveur juste avant NetworkObject.Spawn().
    /// Les NetworkVariables sont incluses dans le message initial de spawn,
    /// donc les clients reçoivent direction + vitesse dès l'apparition de l'objet.
    /// </summary>
    public void SetInitialValues(Vector2 dir, float speed, float projLifetime,
                                  float dmg, float kb, List<AttackEffect> eff)
    {
        netVelocity.Value = dir.normalized * speed;
        lifetime          = projLifetime;
        damage            = dmg;
        knockbackForce    = kb;
        effects           = eff;
    }

    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody2D>();

        // Applique la vélocité initiale sur tous les clients pour un mouvement fluide
        if (rb != null)
        {
            rb.gravityScale   = 0f;
            rb.linearVelocity = netVelocity.Value;
        }

        // Le serveur programme l'auto-despawn après la durée de vie
        if (IsServer && lifetime > 0f)
            StartCoroutine(DespawnAfterLifetime());
    }

    void Update()
    {
        // Si pas de Rigidbody, déplace via transform (fonctionne sur tous les clients)
        if (rb == null && IsSpawned)
            transform.Translate(netVelocity.Value * Time.deltaTime, Space.World);
    }

    private IEnumerator DespawnAfterLifetime()
    {
        yield return new WaitForSeconds(lifetime);
        if (IsSpawned) NetworkObject.Despawn();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Dégâts uniquement sur le serveur
        if (!IsServer || hasHit) return;

        var player = collision.GetComponent<PlayerController>();
        if (player == null) return;

        hasHit = true;

        Vector2 kb = ((Vector2)player.transform.position - (Vector2)transform.position).normalized;
        HitData hitData = new HitData(damage, transform.position, player.transform.position)
            .WithKnockback(kb, knockbackForce);

        if (effects != null)
            foreach (var effect in effects)
                hitData = hitData.WithEffect(effect);

        player.ApplyHit(hitData);

        if (IsSpawned) NetworkObject.Despawn();
    }
}
