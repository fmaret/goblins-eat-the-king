using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Vector3 offset = new Vector3(0, 0, -10);
    [SerializeField] private float smoothSpeed = 0.125f;

    private Transform target;

    void Start()
    {
        StartCoroutine(WaitForLocalPlayer());
    }

    private IEnumerator WaitForLocalPlayer()
    {
        Debug.Log("[CameraFollow] Coroutine démarrée, recherche du joueur local...");
        while (target == null)
        {
            TryFindPlayer();
            yield return new WaitForSeconds(0.1f);
        }
        Debug.Log($"[CameraFollow] Target trouvée : {target.name}");
    }

    void OnDestroy()
    {
        StopAllCoroutines();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
    }

    private void TryFindPlayer()
    {
        foreach (var player in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            var netObj = player.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsOwner)
            {
                target = player.transform;
                break;
            }
        }
    }
}