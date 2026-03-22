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
        if (lobbyPanel == null)
        {
            // GameObject.Find ne trouve pas les objets inactifs, on utilise FindObjectsOfTypeAll
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.name == "LobbyPanel" && go.scene.IsValid())
                {
                    lobbyPanel = go;
                    break;
                }
            }
        }
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
        // Activer le panel AVANT StartClient pour que NGO puisse trouver les NetworkObjects in-scene
        if (lobbyPanel != null) lobbyPanel.SetActive(true);

        // prefer Relay if LobbyManager is configured for it
        if (LobbyManager.Instance != null && LobbyManager.Instance.useRelay)
        {
            try
            {
                ok = await LobbyManager.Instance.StartClientRelayAsync(code);
                if (ok)
                {
                    Debug.Log("JoinModal: successfully joined lobby via Relay");
                    // register this player on the server so the host updates everyone
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
            // lobbyPanel est déjà actif (activé avant StartClient)
        }
        else
        {
            Debug.LogWarning("JoinModal: failed to join lobby (code mismatch or no lobby)");
        }
    }
}
