using UnityEngine;
using Unity.Netcode;
using TMPro;
using UnityEngine.UI;

public class Lobby : MonoBehaviour
{
    [SerializeField] private string lobbyCode;
    [SerializeField] private RectTransform playersSelectionParentContainer;
    [SerializeField] private GameObject playerSelectionPrefab; // prefab should contain a TextMeshProUGUI for name

    // Populate the playersSelectionParentContainer with one LobbyPlayerSelection instance per connected player
    public void DisplayLobby()
    {
        if (playersSelectionParentContainer == null || playerSelectionPrefab == null)
        {
            Debug.LogWarning("Lobby.DisplayLobby: parent container or prefab not assigned");
            return;
        }

        // Robustly clear existing children
        while (playersSelectionParentContainer.childCount > 0)
        {
            var child = playersSelectionParentContainer.GetChild(0);
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }

        // If Netcode is not running, nothing to display
        if (NetworkManager.Singleton == null)
        {
            Debug.Log("Lobby.DisplayLobby: NetworkManager not available");
            return;
        }

        var clients = NetworkManager.Singleton.ConnectedClientsList;
        int idx = 0;
        foreach (var client in clients)
        {
            var go = Instantiate(playerSelectionPrefab, playersSelectionParentContainer);

            // If the prefab has a LobbyPlayerSelection component, use its API
            var selectionComp = go.GetComponent<LobbyPlayerSelection>();
            string displayName = $"Player {client.ClientId}";
            Color displayColor = Color.white;

            if (client.PlayerObject != null)
            {
                // try to extract a visible name from the spawned player object
                var tmpChild = client.PlayerObject.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (tmpChild != null && !string.IsNullOrEmpty(tmpChild.text)) displayName = tmpChild.text;
            }

            if (selectionComp != null)
            {
                selectionComp.SetIndex(idx);
                selectionComp.SetName(displayName);
                selectionComp.SetColor(displayColor);
            }
            else
            {
                // fallback: set text/image on prefab children
                var tmp = go.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (tmp != null) tmp.text = displayName;
                else
                {
                    var t = go.GetComponentInChildren<Text>();
                    if (t != null) t.text = displayName;
                }
                var img = go.GetComponentInChildren<Image>();
                if (img != null) img.color = displayColor;
            }

            idx++;
        }

        // Force layout rebuild so UI updates immediately
        var rt = playersSelectionParentContainer.GetComponent<RectTransform>();
        if (rt != null)
        {
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            Canvas.ForceUpdateCanvases();
        }
    }
}