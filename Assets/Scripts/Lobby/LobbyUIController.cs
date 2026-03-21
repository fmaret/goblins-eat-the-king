using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Goblins.Lobby;

public class LobbyUIController : MonoBehaviour
{
    public TextMeshProUGUI lobbyCodeLabel;
    public Transform playersContainer;
    public GameObject playerItemPrefab; // simple prefab with TMP text and color image

    public TMP_InputField nameInput;
    public Button colorCycleButton;
    public TextMeshProUGUI colorLabel;

    private int colorIndex = 0;
    private List<Color> colors = new List<Color> { Color.white, Color.red, Color.blue, Color.green, Color.yellow };

    void Start()
    {
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.OnPlayersChanged += RefreshPlayers;
        }

        if (colorCycleButton != null) colorCycleButton.onClick.AddListener(CycleColor);
        UpdateColorLabel();
        RefreshPlayers();
    }

    void OnDestroy()
    {
        if (LobbyManager.Instance != null) LobbyManager.Instance.OnPlayersChanged -= RefreshPlayers;
    }

    public void RefreshPlayers()
    {
        if (lobbyCodeLabel != null && LobbyManager.Instance != null)
            lobbyCodeLabel.text = "Code: " + LobbyManager.Instance.lobbyCode;

        if (playersContainer == null || playerItemPrefab == null || LobbyManager.Instance == null) return;
        for (int i = playersContainer.childCount - 1; i >= 0; i--) Destroy(playersContainer.GetChild(i).gameObject);

        int idx = 0;
        foreach (var p in LobbyManager.Instance.players)
        {
            var go = Instantiate(playerItemPrefab, playersContainer);
            var txt = go.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null) txt.text = p.name;
            var img = go.GetComponentInChildren<Image>();
            if (img != null) img.color = p.color;
            idx++;
        }
    }

    void CycleColor()
    {
        colorIndex = (colorIndex + 1) % colors.Count;
        UpdateColorLabel();
    }

    void UpdateColorLabel()
    {
        if (colorLabel != null) colorLabel.text = colors[colorIndex].ToString();
    }

    // apply local name/color for first available slot (host is index 0)
    public void ApplyLocalInfo()
    {
        if (LobbyManager.Instance == null) return;
        string name = nameInput != null ? nameInput.text : "Player";
        Color col = colors[colorIndex];
        int index = LobbyManager.Instance.players.Count > 0 ? 0 : 0;
        LobbyManager.Instance.SetLocalPlayerInfo(index, name, col);
    }
}
