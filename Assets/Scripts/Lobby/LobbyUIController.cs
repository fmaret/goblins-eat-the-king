using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Goblins.Lobby;
using Goblins.Localization;

public class LobbyUIController : MonoBehaviour
{
    public TextMeshProUGUI lobbyCodeLabel;
    public Transform playersContainer;
    public GameObject playerItemPrefab; // simple prefab with TMP text and color image

    public TMP_InputField nameInput;
    public Button colorCycleButton;
    public TextMeshProUGUI colorLabel;

    void Start()
    {
        if (LobbyManager.Instance != null)
            LobbyManager.Instance.OnPlayersChanged += RefreshPlayers;
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += RefreshPlayers;
        if (colorCycleButton != null) colorCycleButton.onClick.AddListener(CycleColor);
        RefreshPlayers();
    }

    void OnDestroy()
    {
        if (LobbyManager.Instance != null) LobbyManager.Instance.OnPlayersChanged -= RefreshPlayers;
        if (LocalizationManager.Instance != null) LocalizationManager.Instance.OnLanguageChanged -= RefreshPlayers;
    }

    public void RefreshPlayers()
    {
        if (lobbyCodeLabel != null && LobbyManager.Instance != null)
        {
            var lm = LocalizationManager.Instance;
            string prefix = lm != null ? lm.Translate("LobbyCode") : "Code";
            lobbyCodeLabel.text = prefix + ": " + LobbyManager.Instance.lobbyCode;
        }

        if (playersContainer == null || playerItemPrefab == null || LobbyManager.Instance == null) return;
        for (int i = playersContainer.childCount - 1; i >= 0; i--) Destroy(playersContainer.GetChild(i).gameObject);

        foreach (var p in LobbyManager.Instance.players)
        {
            var go = Instantiate(playerItemPrefab, playersContainer);
            var sel = go.GetComponent<LobbyPlayerSelection>();
            if (sel != null)
                sel.Initialize(p.ClientId, p.name, p.colorIndex);
            else
            {
                var txt = go.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null) txt.text = p.name;
                var img = go.GetComponentInChildren<Image>();
                if (img != null) img.color = p.color;
            }
        }
    }

    void CycleColor()
    {
        if (LobbyManager.Instance != null)
            LobbyManager.Instance.RequestColorChangeServerRpc(1);
    }

    // apply local name for the local player
    public void ApplyLocalInfo()
    {
        if (LobbyManager.Instance == null) return;
        string name = nameInput != null ? nameInput.text : "Player";
        if (!string.IsNullOrWhiteSpace(name))
            LobbyManager.Instance.RequestNameChangeServerRpc(name);
    }
}
