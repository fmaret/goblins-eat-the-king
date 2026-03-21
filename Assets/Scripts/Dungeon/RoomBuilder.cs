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

    [Header("Triggers de porte (objets existants dans le prefab)")]
    [SerializeField] private GameObject triggerNorth;
    [SerializeField] private GameObject triggerSouth;
    [SerializeField] private GameObject triggerEast;
    [SerializeField] private GameObject triggerWest;

    public void Build(RoomInfo info)
    {
        // Murs et ouvertures
        SetWall(wallNorth, doorNorth, info.openNorth);
        SetWall(wallSouth, doorSouth, info.openSouth);
        SetWall(wallEast,  doorEast,  info.openEast);
        SetWall(wallWest,  doorWest,  info.openWest);

        // Configure DoorTrigger sur les objets trigger existants du prefab
        if (info.openNorth) SetupDoorTrigger(triggerNorth, info.x, info.y, 0);
        if (info.openSouth) SetupDoorTrigger(triggerSouth, info.x, info.y, 1);
        if (info.openEast)  SetupDoorTrigger(triggerEast,  info.x, info.y, 2);
        if (info.openWest)  SetupDoorTrigger(triggerWest,  info.x, info.y, 3);

        // Zone de détection d'entrée dans la salle (spawn ennemis)
        float triggerSize = DungeonGenerator.Instance != null
            ? DungeonGenerator.Instance.RoomSize * 0.2f
            : 4f;
        var entranceGO = new GameObject("RoomEntranceTrigger");
        entranceGO.transform.SetParent(transform, false);
        var entranceCol = entranceGO.AddComponent<BoxCollider2D>();
        entranceCol.isTrigger = true;
        entranceCol.size = new Vector2(triggerSize, triggerSize);
        var ert = entranceGO.AddComponent<RoomEntranceTrigger>();
        ert.roomX = info.x;
        ert.roomY = info.y;

        // Couleur selon le type
        if (floor != null)
        {
            floor.color = info.type == RoomType.Boss
                ? new Color(0.6f, 0.1f, 0.1f) // rouge foncé pour le boss
                : new Color(1f, 1f, 1f); // gris pour les salles normales
        }

        // Décor et interactables
        if (TryGetComponent<RoomFurnisher>(out var furnisher))
        {
            var gen = DungeonGenerator.Instance;
            furnisher.Furnish(info, gen != null ? gen.RoomSize : 10f, gen != null ? gen.DungeonSeed : 0);
        }

    }

    private void SetWall(GameObject wall, GameObject door, bool isOpen)
    {
        if (wall != null) wall.SetActive(!isOpen);
        if (door != null) door.SetActive(isOpen);
    }

    // Configure DoorTrigger sur un objet trigger existant du prefab (pas de création d'enfant)
    private void SetupDoorTrigger(GameObject triggerObj, int x, int y, int dir)
    {
        if (triggerObj == null) return;
        var dt = triggerObj.GetComponent<DoorTrigger>();
        if (dt == null) dt = triggerObj.AddComponent<DoorTrigger>();
        dt.roomX    = x;
        dt.roomY    = y;
        dt.direction = dir;
    }

    // Retourne la position monde du trigger d'entrée pour ce côté
    public Vector3 GetTriggerWorldPosition(int direction)
    {
        GameObject t = direction switch
        {
            0 => triggerNorth, 1 => triggerSouth,
            2 => triggerEast,  3 => triggerWest,
            _ => null
        };
        if (t == null) return transform.position;
        var col = t.GetComponent<BoxCollider2D>();
        return col != null ? col.bounds.center : t.transform.position;
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

    // Referme la porte derrière le joueur (porte visible, mur caché)
    public void CloseDoor(int direction)
    {
        switch (direction)
        {
            case 0: if (wallNorth != null) wallNorth.SetActive(false); if (doorNorth != null) doorNorth.SetActive(true); break;
            case 1: if (wallSouth != null) wallSouth.SetActive(false); if (doorSouth != null) doorSouth.SetActive(true); break;
            case 2: if (wallEast  != null) wallEast .SetActive(false); if (doorEast  != null) doorEast .SetActive(true); break;
            case 3: if (wallWest  != null) wallWest .SetActive(false); if (doorWest  != null) doorWest .SetActive(true); break;
        }
    }
}