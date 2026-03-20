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
}