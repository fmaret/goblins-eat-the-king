using Unity.Netcode;
using UnityEngine;

public class DungeonGenerator : NetworkBehaviour
{
    [Header("Config")]
    [SerializeField] private int gridWidth = 4;
    [SerializeField] private int gridHeight = 4;
    [SerializeField] private float roomSize = 10f;

    [Header("Prefabs salles")]
    [SerializeField] private GameObject roomPrefab;

    [Header("Elements")]
    [SerializeField] private GameObject[] elementPrefabs; // piege, coffre, table

    private RoomInfo bossRoom;

    // Seed synchronisée — le host la génère, le client la reçoit
    private NetworkVariable<int> dungeonSeed = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Grille : true = salle présente
    private RoomInfo[,] grid;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Le host génère une seed aléatoire
            dungeonSeed.Value = Random.Range(1, 999999);
            GenerateDungeon(dungeonSeed.Value);
        }
        else
        {
            // Le client attend la seed puis génère la même map
            dungeonSeed.OnValueChanged += OnSeedReceived;

            // Si la seed est déjà là (timing)
            if (dungeonSeed.Value != 0)
                GenerateDungeon(dungeonSeed.Value);
        }
    }

    private void OnSeedReceived(int oldVal, int newVal)
    {
        if (newVal != 0)
            GenerateDungeon(newVal);
    }

    private void GenerateDungeon(int seed)
    {
        Random.InitState(seed);

        // Boss dans la rangée du haut, colonne aléatoire
        int bossX = Random.Range(0, gridWidth);

        // Grille 4x5 — rangée 0 = rangée boss
        grid = new RoomInfo[gridWidth, gridHeight + 1];

        // Rangée boss (y=0) — seulement la colonne bossX
        grid[bossX, 0] = new RoomInfo
        {
            x = bossX,
            y = 0,
            type = RoomType.Boss,
            openNorth = false,
            openSouth = true,
            openEast = false,
            openWest = false
        };

        // Grille normale 4x4 (y=1 à y=4)
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 1; y <= gridHeight; y++)
            {
                grid[x, y] = new RoomInfo
                {
                    x = x,
                    y = y,
                    type = RoomType.Normal,
                    // Nord : ouvert si salle voisine existe au nord
                    // y==1 → voisine du nord = boss uniquement si même colonne
                    // y>1  → voisine du nord = salle normale toujours présente
                    openNorth = y > 1 || x == bossX,
                    openSouth = y < gridHeight,
                    openEast = x < gridWidth - 1,
                    openWest = x > 0
                };
            }
        }

        BuildDungeon();
    }

    private void BuildDungeon()
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y <= gridHeight; y++)
            {
                // Skip les cases vides de la rangée boss
                if (y == 0 && grid[x, y] == null) continue;
                if (grid[x, y] == null) continue;

                Vector3 pos = new Vector3(x * roomSize, -y * roomSize, 0);
                GameObject room = Instantiate(roomPrefab, pos, Quaternion.identity);
                room.name = y == 0 ? "Room_Boss" : $"Room_{x}_{y}";
                room.GetComponent<RoomBuilder>().Build(grid[x, y], elementPrefabs);
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        dungeonSeed.OnValueChanged -= OnSeedReceived;
    }
}

// Structure de données d'une salle
public class RoomInfo
{
    public int x, y;
    public RoomType type;
    public bool openNorth, openSouth, openEast, openWest;
}