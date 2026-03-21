using UnityEngine;

// Placé automatiquement sur chaque porte ouverte par RoomBuilder.
// Nécessite un Collider2D enfant (créé par RoomBuilder) en mode isTrigger.
public class DoorTrigger : MonoBehaviour
{
    [HideInInspector] public int roomX, roomY, direction; // 0=N 1=S 2=E 3=W

    private void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player == null || !player.IsOwner) return;
        if (DungeonGenerator.Instance == null) return;

        // Ouvre la porte (si la salle courante est cleared)
        DungeonGenerator.Instance.OpenDoorServerRpc(roomX, roomY, direction);

        // Notifie l'entrée dans la salle adjacente avec la direction d'entrée
        (int nx, int ny, int entryDir) = direction switch
        {
            0 => (roomX,     roomY - 1, 1), // part vers le Nord  → entre par le Sud de la salle voisine
            1 => (roomX,     roomY + 1, 0), // part vers le Sud   → entre par le Nord
            2 => (roomX + 1, roomY,     3), // part vers l'Est    → entre par l'Ouest
            3 => (roomX - 1, roomY,     2), // part vers l'Ouest  → entre par l'Est
            _ => (roomX,     roomY,    -1)
        };
        DungeonGenerator.Instance.EnterRoomServerRpc(nx, ny, entryDir);
    }
}
