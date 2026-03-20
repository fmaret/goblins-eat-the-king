using UnityEngine;

public class RoomBuilder : MonoBehaviour
{
    [Header("Murs")]
    [SerializeField] private GameObject wallNorth;
    [SerializeField] private GameObject wallSouth;
    [SerializeField] private GameObject wallEast;
    [SerializeField] private GameObject wallWest;

    [Header("Ouvertures")]
    [SerializeField] private GameObject doorNorth;
    [SerializeField] private GameObject doorSouth;
    [SerializeField] private GameObject doorEast;
    [SerializeField] private GameObject doorWest;

    [Header("Sol")]
    [SerializeField] private SpriteRenderer floor;

    public void Build(RoomInfo info, GameObject[] elementPrefabs)
    {
        // Murs et ouvertures
        SetWall(wallNorth, doorNorth, info.openNorth);
        SetWall(wallSouth, doorSouth, info.openSouth);
        SetWall(wallEast,  doorEast,  info.openEast);
        SetWall(wallWest,  doorWest,  info.openWest);

        // Zones de déclenchement sur chaque porte ouverte
        if (info.openNorth) SetupDoorTrigger(doorNorth, info.x, info.y, 0);
        if (info.openSouth) SetupDoorTrigger(doorSouth, info.x, info.y, 1);
        if (info.openEast)  SetupDoorTrigger(doorEast,  info.x, info.y, 2);
        if (info.openWest)  SetupDoorTrigger(doorWest,  info.x, info.y, 3);

        // Couleur selon le type
        if (floor != null)
        {
            floor.color = info.type == RoomType.Boss
                ? new Color(0.6f, 0.1f, 0.1f) // rouge foncé pour le boss
                : new Color(0.3f, 0.3f, 0.35f); // gris pour les salles normales
        }

        // Spawn éléments aléatoires
        if (elementPrefabs != null && elementPrefabs.Length > 0)
        {
            int count = Random.Range(1, 4);
            for (int i = 0; i < count; i++)
            {
                GameObject prefab = elementPrefabs[Random.Range(0, elementPrefabs.Length)];

                // Position aléatoire dans la salle (évite les bords)
                Vector2 offset = new Vector2(
                    Random.Range(-3.5f, 3.5f),
                    Random.Range(-3.5f, 3.5f)
                );

                Instantiate(prefab, transform.position + (Vector3)offset, Quaternion.identity, transform);
            }
        }
    }

    private void SetWall(GameObject wall, GameObject door, bool isOpen)
    {
        if (wall != null) wall.SetActive(!isOpen);
        if (door != null) door.SetActive(isOpen);
    }

    // Crée une zone trigger enfant sur la porte pour détecter le joueur
    private void SetupDoorTrigger(GameObject door, int x, int y, int dir)
    {
        if (door == null) return;

        var triggerGO = new GameObject("DoorTriggerZone");
        triggerGO.transform.SetParent(door.transform, false);

        var col = triggerGO.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1f, 1f);

        var dt = triggerGO.AddComponent<DoorTrigger>();
        dt.roomX = x;
        dt.roomY = y;
        dt.direction = dir;
    }

    // Appelé par DungeonGenerator pour ouvrir une porte (0=N 1=S 2=E 3=W)
    public void OpenDoor(int direction)
    {
        GameObject door = direction switch
        {
            0 => doorNorth,
            1 => doorSouth,
            2 => doorEast,
            3 => doorWest,
            _ => null
        };
        if (door != null) door.SetActive(false);
    }
}