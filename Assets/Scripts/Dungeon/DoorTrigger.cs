using UnityEngine;

// Placé automatiquement sur chaque porte ouverte par RoomBuilder.
// Nécessite un Collider2D enfant (créé par RoomBuilder) en mode isTrigger.
public class DoorTrigger : MonoBehaviour
{
    [HideInInspector] public int roomX, roomY, direction; // 0=N 1=S 2=E 3=W

    private bool triggered = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered) return;

        var player = other.GetComponent<PlayerController>();
        if (player == null || !player.IsOwner) return;

        triggered = true;

        if (DungeonGenerator.Instance != null)
            DungeonGenerator.Instance.OpenDoorServerRpc(roomX, roomY, direction);
    }
}
