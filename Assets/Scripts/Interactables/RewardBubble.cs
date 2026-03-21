using Unity.Netcode;
using UnityEngine;

// Spawner sur le serveur quand la dernière salle est vidée.
// Le premier joueur qui entre dans le rayon déclenche la récompense.
public class RewardBubble : NetworkBehaviour
{
    [SerializeField] private float attractRadius = 4f;
    [SerializeField] private float attractSpeed = 5f;
    [SerializeField] private float collectRadius = 0.5f;

    [SerializeField] private float attractDelay = 1f;

    private bool _collected;
    private float _spawnTime;
    private Transform _target;

    private void OnEnable() => _spawnTime = Time.time;

    private void Update()
    {
        if (_collected) return;
        if (Time.time - _spawnTime < attractDelay) return;

        // Côté client : cherche le joueur owner le plus proche
        if (_target == null)
        {
            foreach (var player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                if (!player.IsOwner) continue;
                if (Vector2.Distance(transform.position, player.transform.position) <= attractRadius)
                {
                    _target = player.transform;
                    break;
                }
            }
        }

        if (_target == null) return;

        transform.position = Vector2.MoveTowards(transform.position, _target.position, attractSpeed * Time.deltaTime);

        if (Vector2.Distance(transform.position, _target.position) <= collectRadius)
        {
            _collected = true;
            CollectServerRpc(NetworkManager.Singleton.LocalClientId);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void CollectServerRpc(ulong collectorClientId)
    {
        // Notifie le client collecteur avant de despawn
        CollectedClientRpc(collectorClientId);
        GetComponent<NetworkObject>().Despawn();
    }

    [ClientRpc]
    private void CollectedClientRpc(ulong collectorClientId)
    {
        if (NetworkManager.Singleton.LocalClientId != collectorClientId) return;
        Debug.Log($"[RewardBubble] Collectée par le client {collectorClientId} !");
        UpgradeChoice.Instance.GenerateNewChoices();
        UpgradeChoice.Instance.SetActive(true);
    }
}
