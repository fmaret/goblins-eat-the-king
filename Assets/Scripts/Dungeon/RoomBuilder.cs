using UnityEngine;

public class RoomBuilder : MonoBehaviour
{
    [Header("Murs")]
    [SerializeField] private GameObject wallNorthFull, wallSouthFull, wallEastFull, wallWestFull;

    [Header("Murs avec porte")]
    [SerializeField] private GameObject wallNorthL, wallNorthR, doorNorth;
    [SerializeField] private GameObject wallSouthL, wallSouthR, doorSouth;
    [SerializeField] private GameObject wallEastL, wallEastR, doorEast;
    [SerializeField] private GameObject wallWestL, wallWestR, doorWest;

    [Header("Sol")]
    [SerializeField] private SpriteRenderer floor;

    public void Build(RoomInfo info, GameObject[] elementPrefabs)
    {
        SetWall(wallNorthFull, wallNorthL, wallNorthR, doorNorth, info.openNorth);
        SetWall(wallSouthFull, wallSouthL, wallSouthR, doorSouth, info.openSouth);
        SetWall(wallEastFull, wallEastL, wallEastR, doorEast, info.openEast);
        SetWall(wallWestFull, wallWestL, wallWestR, doorWest, info.openWest);

        if (floor != null)
            floor.color = info.type == RoomType.Boss
                ? new Color(0.6f, 0.1f, 0.1f)
                : new Color(0.3f, 0.3f, 0.35f);

        if (elementPrefabs != null && elementPrefabs.Length > 0)
        {
            int count = Random.Range(1, 4);
            for (int i = 0; i < count; i++)
            {
                GameObject prefab = elementPrefabs[Random.Range(0, elementPrefabs.Length)];
                Vector2 offset = new Vector2(Random.Range(-3.5f, 3.5f), Random.Range(-3.5f, 3.5f));
                Instantiate(prefab, transform.position + (Vector3)offset, Quaternion.identity, transform);
            }
        }
    }

    private void SetWall(GameObject full, GameObject partL, GameObject partR, GameObject door, bool isOpen)
    {
        // Mur plein visible uniquement si fermé
        if (full != null) full.SetActive(!isOpen);
        // Moitiés + porte visibles uniquement si ouvert
        if (partL != null) partL.SetActive(isOpen);
        if (partR != null) partR.SetActive(isOpen);
        if (door != null) door.SetActive(isOpen);
    }
}