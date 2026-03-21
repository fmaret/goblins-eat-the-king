using UnityEngine;
using UnityEngine.UI;

// Attacher sur un GameObject avec un Canvas en Screen Space.
// Requiert un Panel enfant avec deux boutons (Oui / Non).
public class DoorPromptUI : MonoBehaviour
{
    public static DoorPromptUI Instance { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    private int _toX, _toY, _entryDir;


    private void Awake()
    {
        Instance = this;
        panel.SetActive(false);
        yesButton.onClick.AddListener(OnYes);
        noButton.onClick.AddListener(OnNo);
    }

    public void Show(int toX, int toY, int entryDir)
    {
        _toX = toX; _toY = toY; _entryDir = entryDir;
        panel.SetActive(true);
    }

    public void Hide() => panel.SetActive(false);

    private void OnYes()
    {
        Hide();

        var gen = DungeonGenerator.Instance;
        if (gen == null) return;

        // Téléportation locale (ClientNetworkTransform = seul l'owner bouge son joueur)
        Vector3 target = gen.GetTriggerWorldPositionForRoom(_toX, _toY, _entryDir);
        foreach (var player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (!player.IsOwner) continue;
            player.transform.position = target;
            break;
        }

        // Démarre la musique de combat si la salle n'est pas encore vidée
        if (!gen.IsRoomCleared(_toX, _toY) && SoundManager.Instance != null)
            SoundManager.Instance.PlayFightMusic(gen.IsRoomBoss(_toX, _toY));

        gen.EnterRoomFromDoor(_toX, _toY, _entryDir);
    }

    private void OnNo() => Hide();
}
