using System.Collections;
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

    [Header("Ennemis")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private int enemiesPerRoom = 10;

    [Header("Spawn joueur")]
    [SerializeField] private Vector2Int spawnCell = new Vector2Int(0, 2);

    public float RoomSize    => roomSize;
    public int   DungeonSeed => dungeonSeed.Value;
    public Vector3 SpawnPosition => new Vector3(spawnCell.x * roomSize, -spawnCell.y * roomSize, 0);

    // Seed synchronisée — le host la génère, le client la reçoit
    private NetworkVariable<int> dungeonSeed = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Grille : true = salle présente
    private RoomInfo[,] grid;

    // Référence aux RoomBuilders pour ouvrir les portes
    private readonly Dictionary<(int, int), RoomBuilder>  rooms      = new();
    private readonly Dictionary<(int, int), RoomFurnisher> furnishers = new();

    // Gestion ennemis par salle (server only)
    private readonly HashSet<(int, int)> roomEntered = new();
    private readonly Dictionary<(int, int), int> roomEnemyCounts = new();
    private readonly HashSet<(int, int)> roomCleared = new();
    private readonly Dictionary<(int, int), int> roomEntryDirection = new(); // direction d'entrée par salle

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
        furnishers.Clear();
        roomEntered.Clear();
        roomEnemyCounts.Clear();
        roomCleared.Clear();
        roomEntryDirection.Clear();

        // Salle de départ pré-cleared sur tous les clients
        roomEntered.Add((spawnCell.x, spawnCell.y));
        roomCleared.Add((spawnCell.x, spawnCell.y));

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
                builder.Build(grid[x, y]);
                rooms[(x, y)] = builder;
                if (room.TryGetComponent<RoomFurnisher>(out var furnisher))
                    furnishers[(x, y)] = furnisher;
            }
        }
    }

    // Appelé par DoorTrigger (entryDirection = côté d'entrée dans la nouvelle salle)
    // ou RoomEntranceTrigger pour la salle de départ (entryDirection = -1)
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void EnterRoomServerRpc(int x, int y, int entryDirection)
    {
        var key = (x, y);

        // Ferme toujours la porte derrière le joueur (même si J2 passe après J1)
        if (entryDirection >= 0)
        {
            (int px, int py) = entryDirection switch
            {
                0 => (x, y - 1), 1 => (x, y + 1),
                2 => (x + 1, y), 3 => (x - 1, y),
                _ => (x, y)
            };
            CloseDoorClientRpc(x, y, entryDirection);
            CloseDoorClientRpc(px, py, entryDirection ^ 1);
        }

        if (roomEntered.Contains(key)) return;
        roomEntered.Add(key);

        // Mémorise la direction d'entrée (pour rouvrir les bonnes portes au clear)
        if (entryDirection >= 0)
            roomEntryDirection[key] = entryDirection;

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
            StartCoroutine(SpawnEnemiesProgressively(x, y, entryDirection));
        }
    }

    private IEnumerator SpawnEnemiesProgressively(int x, int y, int entryDirection)
    {
        var info = grid[x, y];
        float cx = x * roomSize;
        float cy = -y * roomSize;
        float d  = roomSize * 0.45f;

        // Points de spawn = portes ouvertes SAUF la porte d'entrée
        var spawnPoints = new List<Vector3>();
        if (info.openNorth && entryDirection != 0) spawnPoints.Add(new Vector3(cx,     cy + d, 0f));
        if (info.openSouth && entryDirection != 1) spawnPoints.Add(new Vector3(cx,     cy - d, 0f));
        if (info.openEast  && entryDirection != 2) spawnPoints.Add(new Vector3(cx + d, cy,     0f));
        if (info.openWest  && entryDirection != 3) spawnPoints.Add(new Vector3(cx - d, cy,     0f));

        // Fallback : centre de la salle (salle de départ ou aucune autre porte)
        if (spawnPoints.Count == 0)
            spawnPoints.Add(new Vector3(cx, cy, 0f));

        for (int i = 0; i < enemiesPerRoom; i++)
        {
            yield return new WaitForSeconds(0.5f);

            Vector3 origin = spawnPoints[Random.Range(0, spawnPoints.Count)];
            Vector2 offset = Random.insideUnitCircle * 0.6f;
            Vector3 pos    = origin + new Vector3(offset.x, offset.y, 0f);

            var go = Instantiate(enemyPrefab, pos, Quaternion.identity);
            var ec = go.GetComponent<EnemyController>();
            ec.SetRoom(x, y);
            go.GetComponent<NetworkObject>().Spawn();
        }
    }

    // Appelé par EnemyController quand un ennemi meurt (server only)
    public void NotifyEnemyDied(int x, int y)
    {
        var key = (x, y);
        if (!roomEnemyCounts.ContainsKey(key)) return;
        roomEnemyCounts[key]--;
        if (roomEnemyCounts[key] > 0) return;

        roomCleared.Add(key);
        SyncRoomClearedClientRpc(x, y);

        // Si un joueur est déjà dans un trigger de porte, ouvre-la immédiatement
        OpenDoorIfPlayerPresent(x, y);
    }

    private void OpenDoorIfPlayerPresent(int x, int y)
    {
        var info = grid[x, y];
        if (info == null) return;
        float cx = x * roomSize, cy = -y * roomSize;
        float d = roomSize * 0.45f;

        if (info.openNorth) CheckDoorOverlap(x, y, 0, cx,     cy + d);
        if (info.openSouth) CheckDoorOverlap(x, y, 1, cx,     cy - d);
        if (info.openEast)  CheckDoorOverlap(x, y, 2, cx + d, cy    );
        if (info.openWest)  CheckDoorOverlap(x, y, 3, cx - d, cy    );
    }

    public bool IsRoomCleared(int x, int y) => roomCleared.Contains((x, y));

    private void CheckDoorOverlap(int x, int y, int dir, float doorX, float doorY)
    {
        var hits = Physics2D.OverlapBoxAll(new Vector2(doorX, doorY), new Vector2(1.5f, 1.5f), 0f);
        foreach (var hit in hits)
        {
            if (hit.GetComponent<PlayerController>() != null)
            {
                OpenDoorClientRpc(x, y, dir);
                return;
            }
        }
    }

    // Appelé par DoorPromptUI (Oui) : entre dans la salle (la téléportation est faite côté client)
    public void EnterRoomFromDoor(int toX, int toY, int entryDir)
    {
        EnterRoomServerRpc(toX, toY, entryDir);
    }

    // Retourne la position monde du trigger d'entrée pour une salle donnée (appelé côté client owner)
    public Vector3 GetTriggerWorldPositionForRoom(int x, int y, int dir)
    {
        if (rooms.TryGetValue((x, y), out var builder))
            return builder.GetTriggerWorldPosition(dir);
        return new Vector3(x * roomSize, -y * roomSize, 0f);
    }

    // Synchronise l'état cleared vers tous les clients
    [ClientRpc]
    private void SyncRoomClearedClientRpc(int x, int y)
    {
        roomCleared.Add((x, y));
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

    [ClientRpc]
    private void CloseDoorClientRpc(int x, int y, int direction)
    {
        if (rooms.TryGetValue((x, y), out var builder))
            builder.CloseDoor(direction);
    }

    // --- Pots ---

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void BreakPotServerRpc(int roomX, int roomY, int potIndex)
        => BreakPotClientRpc(roomX, roomY, potIndex);

    [ClientRpc]
    private void BreakPotClientRpc(int roomX, int roomY, int potIndex)
    {
        if (furnishers.TryGetValue((roomX, roomY), out var f))
            f.BreakPot(potIndex);
    }

    // --- Coffres ---

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void OpenChestServerRpc(int roomX, int roomY)
        => OpenChestClientRpc(roomX, roomY);

    [ClientRpc]
    private void OpenChestClientRpc(int roomX, int roomY)
    {
        if (furnishers.TryGetValue((roomX, roomY), out var f))
            f.OpenChest();
    }

    public override void OnNetworkDespawn()
    {
        dungeonSeed.OnValueChanged -= OnSeedReceived;
    }
}

public enum RoomType { Normal, Boss }

// Structure de données d'une salle
public class RoomInfo
{
    public int x, y;
    public RoomType type;
    public bool openNorth, openSouth, openEast, openWest;
}
