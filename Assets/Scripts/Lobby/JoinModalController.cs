using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Goblins.Lobby;

public class JoinModalController : MonoBehaviour
{
    public TMP_InputField codeInput;
    public Button colorCycleButton;
    public TextMeshProUGUI colorLabel;
    public Button validateButton;
    public GameObject lobbyPanel; // assign or find

    private int colorIndex = 0;
    private Color[] colors = new Color[] { Color.white, Color.red, Color.blue, Color.green };

    void Start()
    {
        if (colorCycleButton != null) colorCycleButton.onClick.AddListener(CycleColor);
        if (validateButton != null) {
            validateButton.onClick.AddListener(OnValidate);
            Debug.Log("JoinModalController: hooked validateButton listener");
        }
        UpdateColorLabel();
        if (lobbyPanel == null) lobbyPanel = GameObject.Find("LobbyPanel");
    }

    void CycleColor()
    {
        colorIndex = (colorIndex + 1) % colors.Length;
        UpdateColorLabel();
    }

    void UpdateColorLabel()
    {
        if (colorLabel != null) colorLabel.text = colors[colorIndex].ToString();
    }

    async void OnValidate()
    {
        if (codeInput == null) return;
        var code = codeInput.text.Trim();
        if (string.IsNullOrEmpty(code)) return;

        bool ok = false;
        // prefer Relay if LobbyManager is configured for it
        if (LobbyManager.Instance != null && LobbyManager.Instance.useRelay)
        {
            try
            {
                ok = await LobbyManager.Instance.StartClientRelayAsync(code);
                if (ok)
                {
                    // register this player on the server so the host updates everyone
                    LobbyManager.Instance.RegisterPlayerInfoServerRpc("Player", colors[colorIndex].r, colors[colorIndex].g, colors[colorIndex].b, colors[colorIndex].a);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("JoinModal: StartClientRelayAsync failed: " + ex.Message);
                ok = false;
            }
        }

        // if relay not used or failed, try local join
        if (!ok && LobbyManager.Instance != null)
        {
            ok = LobbyManager.Instance.JoinLobby(code, colors[colorIndex]);
        }

        // final fallback: try editor-local registry directly (in case Instance isn't present)
        #if UNITY_EDITOR
        if (!ok)
        {
            ok = Goblins.Lobby.EditorLocalLobbyRegistry.TryJoin(code, new Goblins.Lobby.LobbyPlayer { name = "Player", color = colors[colorIndex] });
        }
        #endif

        if (ok)
        {
            this.gameObject.SetActive(false);
            if (lobbyPanel != null) lobbyPanel.SetActive(true);
        }
        else
        {
            Debug.LogWarning("JoinModal: failed to join lobby (code mismatch or no lobby)");
        }
    }
}
