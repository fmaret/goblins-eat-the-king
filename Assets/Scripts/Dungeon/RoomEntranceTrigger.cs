using UnityEngine;

// Placé automatiquement au centre de chaque salle par RoomBuilder.
// Quand un joueur local entre, envoie un ServerRpc pour spawner les ennemis.
public class RoomEntranceTrigger : MonoBehaviour
{
    [HideInInspector] public int roomX, roomY;
    private bool triggered = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered) return;
        var player = other.GetComponent<PlayerController>();
        if (player == null || !player.IsOwner) return;
        triggered = true;
        DungeonGenerator.Instance.EnterRoomServerRpc(roomX, roomY, -1);
    }
}
    