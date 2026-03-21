using UnityEngine;
using TMPro;

public class DisplayStats : MonoBehaviour
{
    public static DisplayStats Instance { get; private set; }

    void Awake()
    {
        Instance = this;
        SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    [Header("References")]
    [SerializeField] private GameObject statsPanel; // panel to display stats
    // No prefab required: create a TextMeshPro (3D) object at runtime

    public bool IsOpen => statsPanel != null && statsPanel.activeSelf;

    public void SetActive(bool active)
    {
        if (this != null && statsPanel != null)
            statsPanel.SetActive(active);
    }

    public void DisplayPlayerStats(PlayerController playerController)
    {
        Debug.Log("DisplayStats: Displaying player stats for player " + playerController?.name);
        if (playerController == null)
        {
            Debug.LogWarning("DisplayStats: no player controller assigned");
            return;
        }
        if (statsPanel != null) {
            for (int i = statsPanel.transform.childCount - 1; i >= 0; i--)
                Destroy(statsPanel.transform.GetChild(i).gameObject);
        }
        var stats = playerController.GetStats();
        Debug.Log("DisplayStats: Player stats: " + stats);
        var go = new GameObject("StatsText");
        if (statsPanel != null)
            go.transform.SetParent(statsPanel.transform, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 30;
        tmp.text = stats;
    }
}