using UnityEngine;

// Placé automatiquement sur chaque porte ouverte par RoomBuilder.
// Affiche une modale Oui/Non quand le joueur local entre dans la zone.
public class DoorTrigger : MonoBehaviour
{
    [HideInInspector] public int roomX, roomY, direction; // 0=N 1=S 2=E 3=W

    // Pas de singleton statique : en MPPM les statics sont partagés entre joueurs,
    // FindObjectOfType cherche dans le scène-graph du joueur local uniquement.
    private static DoorPromptUI FindPromptUI() => FindFirstObjectByType<DoorPromptUI>();

    private void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player == null || !player.IsOwner) return;
        if (DungeonGenerator.Instance == null) return;
        if (!DungeonGenerator.Instance.IsRoomCleared(roomX, roomY)) return;

        (int nx, int ny, int entryDir) = direction switch
        {
            0 => (roomX,     roomY - 1, 1),
            1 => (roomX,     roomY + 1, 0),
            2 => (roomX + 1, roomY,     3),
            3 => (roomX - 1, roomY,     2),
            _ => (roomX,     roomY,    -1)
        };

        var ui = FindPromptUI();
        if (ui != null) ui.Show(nx, ny, entryDir);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player == null || !player.IsOwner) return;
        var ui = FindPromptUI();
        if (ui != null) ui.Hide();
    }
}
