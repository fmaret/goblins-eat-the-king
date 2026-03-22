using Unity.Netcode;
using UnityEngine;

public class HideIfNetworkActive : MonoBehaviour
{
    void Awake()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            gameObject.SetActive(false);
    }
}
