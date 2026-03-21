using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class DungeonGenerator : NetworkBehaviour
{
    public static DungeonGenerator Instance { get; private set; }

    [Header("Config")]
    [SerializeField] private int gridWidth = 5;
    [SerializeField] private int gridHeight = 5;
    [SerializeField] private float roomSize = 10f;

    [Header("Prefabs salles")]
    [SerializeField] private GameObject roomPrefab;

    [Header("Elements")]
    [SerializeField] private GameObject[] elementPrefabs; // piege, coffre, table

    [Header("Ennemis")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private int enemiesPerRoom = 10;

    [Header("Spawn joueur")]
    [SerializeField] private Vector2Int spawnCell = new Vector2Int(0, 2);

    public float RoomSize => roomSize;
    public Vector3 SpawnPosition => new Vector3(spawnCell.x * roomSize, -spawnCell.y * roomSize, 0);

    private RoomInfo bossRoom;

    // Seed synchronisée — le host la génère, le client la reçoit
    private NetworkVariable<int> dungeonSeed = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Grille : true = salle présente
    private RoomInfo[,] grid;

    // Référence aux RoomBuilders pour ouvrir les portes
    private readonly Dictionary<(int, int), RoomBuilder> rooms = new();

    // Gestion ennemis par salle (server only)
    private readonly HashSet<(int, int)> roomEntered = new();
    private readonly Dictionary<(int, int), int> roomEnemyCounts = new();
    private readonly HashSet<(int, int)> roomCleared = new();

    private void Awake()
    {
        Instance = this;
    }

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
        rooms.Clear();

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

                var builder = room.GetComponent<RoomBuilder>();
                builder.Build(grid[x, y], elementPrefabs);
                rooms[(x, y)] = builder;
            }
        }
    }

    // Appelé par RoomEntranceTrigger quand un joueur entre dans la salle
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void EnterRoomServerRpc(int x, int y)
    {
        var key = (x, y);
        if (roomEntered.Contains(key)) return;
        roomEntered.Add(key);

        if (enemyPrefab == null) { roomCleared.Add(key); return; }

        bool isBoss = grid[x, y] != null && grid[x, y].type == RoomType.Boss;

        if (isBoss)
        {
            // Un seul boss, scale x2
            roomEnemyCounts[key] = 1;
            Vector3 pos = new Vector3(x * roomSize, -y * roomSize, 0f);
            var go = Instantiate(enemyPrefab, pos, Quaternion.identity);
            go.transform.localScale = Vector3.one * 2f;
            var ec = go.GetComponent<EnemyController>();
            ec.SetRoom(x, y);
            ec.SetStats(200f, 25f);
            go.GetComponent<NetworkObject>().Spawn();
        }
        else
        {
            roomEnemyCounts[key] = enemiesPerRoom;
            for (int i = 0; i < enemiesPerRoom; i++)
            {
                Vector2 offset = Random.insideUnitCircle * (roomSize * 0.3f);
                Vector3 pos = new Vector3(x * roomSize + offset.x, -y * roomSize + offset.y, 0f);
                var go = Instantiate(enemyPrefab, pos, Quaternion.identity);
                var ec = go.GetComponent<EnemyController>();
                ec.SetRoom(x, y);
                go.GetComponent<NetworkObject>().Spawn();
            }
        }
    }

    // Appelé par EnemyController quand un ennemi meurt (server only)
    public void NotifyEnemyDied(int x, int y)
    {
        var key = (x, y);
        if (!roomEnemyCounts.ContainsKey(key)) return;
        roomEnemyCounts[key]--;
        if (roomEnemyCounts[key] <= 0)
            roomCleared.Add(key);
    }

    // Appelé par DoorTrigger sur le client propriétaire
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void OpenDoorServerRpc(int x, int y, int direction)
    {
        if (!roomCleared.Contains((x, y))) return;
        OpenDoorClientRpc(x, y, direction);
    }

    // Synchronise l'ouverture de la porte sur tous les clients (+ la porte opposée)
    [ClientRpc]
    private void OpenDoorClientRpc(int x, int y, int direction)
    {
        if (rooms.TryGetValue((x, y), out var builder))
            builder.OpenDoor(direction);

        // Ouvre aussi le côté opposé dans la salle adjacente
        var (nx, ny, opp) = direction switch
        {
            0 => (x,     y - 1, 1),
            1 => (x,     y + 1, 0),
            2 => (x + 1, y,     3),
            3 => (x - 1, y,     2),
            _ => (x,     y,     direction)
        };
        if (rooms.TryGetValue((nx, ny), out var adjBuilder))
            adjBuilder.OpenDoor(opp);
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
