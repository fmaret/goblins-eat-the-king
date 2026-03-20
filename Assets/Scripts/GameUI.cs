using UnityEngine;
using Unity.Netcode;

public class GameUI : MonoBehaviour
{
    public static GameUI Instance { get; private set; }

    [Header("Player UI")]
    [SerializeField] private StatBar playerHealthBar;
    [Header("Header Players")]
    [SerializeField] private Transform playersHeaderContainer;
    [SerializeField] private GameObject playerInfoPrefab;
    private System.Collections.Generic.Dictionary<ulong, PlayerInfo> playerEntries = new System.Collections.Generic.Dictionary<ulong, PlayerInfo>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetPlayerHealth(float current, float max, string text = null)
    {
        if (playerHealthBar != null)
            playerHealthBar.Set(current, max, text);
    }

    public void AddPlayerEntry(ulong clientId, string displayName)
    {
        Debug.Log($"GameUI: Attempting to add player entry for {clientId} ({displayName}) on local client {NetworkManager.Singleton?.LocalClientId}");
        if (playerInfoPrefab == null || playersHeaderContainer == null) return;
        if (playerEntries.ContainsKey(clientId)) return;
        Debug.Log($"GameUI: Adding player entry for {clientId} ({displayName}) on local client {NetworkManager.Singleton?.LocalClientId}");
        var go = Instantiate(playerInfoPrefab, playersHeaderContainer);
        go.SetActive(true);
        // ensure correct rect transform scale when instantiating UI prefabs
        var rt = go.GetComponent<RectTransform>();
        if (rt != null) rt.localScale = Vector3.one;
        var info = go.GetComponent<PlayerInfo>();
        Debug.Log($"GameUI: Instantiated prefab '{go.name}', activeInHierarchy={go.activeInHierarchy}, parent='{go.transform.parent?.name}'");
        if (info != null)
        {
            info.SetName(displayName);
            info.SetHealth(1f, 1f, "");
            playerEntries.Add(clientId, info);
        }
        else
        {
            Debug.LogWarning("GameUI: instantiated prefab missing PlayerInfo component, destroying instance.");
            Destroy(go);
        }
    }

    public void RemovePlayerEntry(ulong clientId)
    {
        if (!playerEntries.TryGetValue(clientId, out var info)) return;
        if (info != null)
            Destroy(info.gameObject);
        playerEntries.Remove(clientId);
    }

    public void SetPlayerEntryHealth(ulong clientId, float current, float max, string text = null)
    {
        if (!playerEntries.TryGetValue(clientId, out var info))
        {
            // create a fallback entry if the player wasn't registered yet
            AddPlayerEntry(clientId, $"Player {clientId}");
            playerEntries.TryGetValue(clientId, out info);
        }

        if (info != null)
            info.SetHealth(current, max, text);
    }

}
