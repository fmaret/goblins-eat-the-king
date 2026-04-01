using UnityEngine;
using Unity.Netcode;
using System.Collections;

/// <summary>
/// NetworkBehaviour à placer sur le prefab d'aura (ex: AuraHalo).
/// Le serveur le spawne et le parent à l'ennemi — NGO le réplique automatiquement
/// sur tous les clients. L'aura suit l'ennemi grâce au parenting réseau.
///
/// ► Ajouter ce script ET un NetworkObject sur le prefab.
/// ► Enregistrer le prefab dans la NetworkPrefabsList du projet.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NetworkAura : NetworkBehaviour
{
    private float lifetime;

    /// <summary>Appelé sur le serveur avant Spawn() pour définir la durée de vie.</summary>
    public void SetLifetime(float duration) => lifetime = duration;

    public override void OnNetworkSpawn()
    {
        if (IsServer && lifetime > 0f)
            StartCoroutine(DespawnAfterLifetime());
    }

    private IEnumerator DespawnAfterLifetime()
    {
        yield return new WaitForSeconds(lifetime);
        if (IsSpawned) NetworkObject.Despawn();
    }
}
