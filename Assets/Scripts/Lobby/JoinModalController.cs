using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Goblins.Lobby;

public class JoinModalController : MonoBehaviour
{
    public TMP_InputField codeInput;
    public TMP_InputField nameInput;
    public Button colorCycleButton;
    public TextMeshProUGUI colorLabel;
    public Button validateButton;
    public GameObject lobbyPanel; // assign or find

    private int colorIndex = 0;
    private Color[] colors = new Color[] { Color.white, Color.red, Color.blue, Color.green };

    void Start()
    {
        if (colorCycleButton != null) colorCycleButton.onClick.AddListener(CycleColor);
        if (validateButton != null) validateButton.onClick.AddListener(OnValidate);
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

    void OnValidate()
    {
        if (codeInput == null || nameInput == null) return;
        var code = codeInput.text.Trim();
        var name = nameInput.text.Trim();
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(name)) return;

        // attempt local join (works if host exists in same process)
        bool ok = LobbyManager.Instance != null && LobbyManager.Instance.JoinLobby(code, name, colors[colorIndex]);
        if (ok)
        {
            // close modal and open lobby
            this.gameObject.SetActive(false);
            if (lobbyPanel != null) lobbyPanel.SetActive(true);
        }
        else
        {
            Debug.LogWarning("JoinModal: failed to join lobby (code mismatch or no lobby)");
        }
    }
}
