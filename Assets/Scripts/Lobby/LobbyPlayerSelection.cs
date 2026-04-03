using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Goblins.Lobby;

/// <summary>
/// Panneau de sélection d'un joueur dans le lobby.
/// Seul le joueur propriétaire (clientId == LocalClientId) peut modifier son pseudo et sa couleur.
/// Les boutons ◀ / ▶ cyclent les couleurs disponibles en sautant celles déjà prises.
/// </summary>
public class LobbyPlayerSelection : MonoBehaviour
{
    [Header("Références UI")]
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private Button prevColorButton;
    [SerializeField] private Button nextColorButton;
    [SerializeField] private TextMeshProUGUI colorNameLabel;
    [SerializeField] private Image colorPreview;
    [Tooltip("Optionnel : affiche 'Vous' pour le panneau local.")]
    [SerializeField] private TextMeshProUGUI youLabel;

    // ── Identité ────────────────────────────────────────────────────────────
    private ulong ownerClientId;
    private bool isLocalPlayer;


    // ── Initialisation principale ────────────────────────────────────────────

    /// <summary>
    /// Configure le panneau pour le joueur donné.
    /// Seul le joueur dont l'ID correspond au LocalClientId peut interagir.
    /// </summary>
    public void Initialize(ulong clientId, string playerName, int colorIndex)
    {
        ownerClientId = clientId;
        isLocalPlayer = Unity.Netcode.NetworkManager.Singleton != null
                        && Unity.Netcode.NetworkManager.Singleton.LocalClientId == clientId;

        // Affichage initial
        if (nameInput != null) nameInput.text = playerName;
        UpdateColorDisplay(colorIndex);
        if (youLabel != null) youLabel.text = isLocalPlayer ? "Vous" : "";

        // Interactivité — uniquement le joueur propriétaire
        if (nameInput       != null) nameInput.interactable       = isLocalPlayer;
        if (prevColorButton != null) prevColorButton.interactable = isLocalPlayer;
        if (nextColorButton != null) nextColorButton.interactable = isLocalPlayer;

        // Câblage des événements (uniquement pour le joueur local)
        if (!isLocalPlayer) return;

        if (nameInput != null)
        {
            nameInput.onEndEdit.RemoveAllListeners();
            nameInput.onEndEdit.AddListener(OnNameEndEdit);
        }
        if (prevColorButton != null)
        {
            prevColorButton.onClick.RemoveAllListeners();
            prevColorButton.onClick.AddListener(() => RequestColorChange(-1));
        }
        if (nextColorButton != null)
        {
            nextColorButton.onClick.RemoveAllListeners();
            nextColorButton.onClick.AddListener(() => RequestColorChange(1));
        }
    }

    /// <summary>Met à jour l'affichage sans recâbler les événements (lors d'un refresh réseau).</summary>
    public void UpdateDisplay(string playerName, int colorIndex)
    {
        // Ne pas écraser si le joueur est en train de taper son pseudo
        if (nameInput != null && !nameInput.isFocused)
            nameInput.text = playerName;
        UpdateColorDisplay(colorIndex);
    }

    // ── Callbacks UI ────────────────────────────────────────────────────────

    private void OnNameEndEdit(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return;
        // Sauvegarde immédiate locale, sans attendre le round-trip RPC
        if (isLocalPlayer)
            Goblins.Lobby.LobbyManager.LocalPlayerName = newName;
        if (LobbyManager.Instance != null)
            LobbyManager.Instance.RequestNameChangeServerRpc(newName);
    }

    private void RequestColorChange(int direction)
    {
        if (LobbyManager.Instance != null)
            LobbyManager.Instance.RequestColorChangeServerRpc(direction);
    }

    // ── Affichage couleur ────────────────────────────────────────────────────

    private void UpdateColorDisplay(int idx)
    {
        if (idx < 0 || idx >= LobbyManager.ColorPalette.Length) return;
        if (colorPreview   != null) colorPreview.color  = LobbyManager.ColorPalette[idx];
        if (colorNameLabel != null) colorNameLabel.text = LobbyManager.ColorNames[idx];
    }
}
