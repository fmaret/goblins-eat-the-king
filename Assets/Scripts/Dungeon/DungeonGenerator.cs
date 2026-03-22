using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class DungeonGenerator : NetworkBehaviour
{
    public static DungeonGenerator Instance { get; private set; }

    [Header("Config")]
    [SerializeField] private int gridWidth      = 7;
    [SerializeField] private int gridHeight     = 7;
    [SerializeField] private int targetRoomCount = 14;
    [SerializeField] private float roomSize     = 10f;

    [Header("Prefabs salles")]
    [SerializeField] private GameObject roomPrefab;

    [Header("Ennemis")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private int   minEnemiesPerRoom  = 3;
    [SerializeField] private int   maxEnemiesPerRoom  = 8;
    [SerializeField] private float enemySpawnInterval = 0.8f;
    [SerializeField] private RuntimeAnimatorController bossAnimatorController;

    [Header("Récompense de salle")]
    [SerializeField] private GameObject rewardBubblePrefab;
    [SerializeField] private GameObject levelCompletePrefab;

    [Header("Spawn joueur")]
    [SerializeField] private Vector2Int spawnCell = new Vector2Int(3, 6);

    public float RoomSize    => roomSize;
    public int   DungeonSeed => dungeonSeed.Value;
    public int   GridWidth   => gridWidth;
    public int   GridHeight  => gridHeight;
    public Vector3 SpawnPosition => new Vector3(spawnCell.x * roomSize, -spawnCell.y * roomSize, 0);

    public RoomInfo GetRoom(int x, int y)
    {
        if (grid == null || x < 0 || x >= gridWidth || y < 0 || y >= gridHeight + 1) return null;
        return grid[x, y];
    }
    public bool IsRoomEntered(int x, int y) => roomEntered.Contains((x, y));
    public bool HasChest(int x, int y)      => chestRooms.Contains((x, y));
    public void RegisterChestRoom(int x, int y) => chestRooms.Add((x, y));

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
    private readonly HashSet<(int, int)> chestRooms  = new();
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
        var rng = new System.Random(seed);

        grid = new RoomInfo[gridWidth, gridHeight + 1];

        // ── 1. Growing tree : forme organique ────────────────────────────────
        var placed   = new HashSet<(int, int)>();
        var frontier = new List<(int, int)>();

        int startX = gridWidth / 2;
        int startY = (gridHeight + 1) / 2;
        placed.Add((startX, startY));
        frontier.Add((startX, startY));

        int[] dx = {  0,  0, 1, -1 };
        int[] dy = { -1,  1, 0,  0 };

        while (placed.Count < targetRoomCount && frontier.Count > 0)
        {
            // Mix newest/random pour des formes plus variées
            int fi = rng.Next(2) == 0 ? frontier.Count - 1 : rng.Next(frontier.Count);
            var (fx, fy) = frontier[fi];

            int[] dirs = { 0, 1, 2, 3 };
            for (int i = 3; i > 0; i--) { int j = rng.Next(i + 1); (dirs[i], dirs[j]) = (dirs[j], dirs[i]); }

            bool expanded = false;
            foreach (int d in dirs)
            {
                int nx = fx + dx[d], ny = fy + dy[d];
                if (nx >= 0 && nx < gridWidth && ny >= 1 && ny <= gridHeight && !placed.Contains((nx, ny)))
                {
                    placed.Add((nx, ny));
                    frontier.Add((nx, ny));
                    expanded = true;
                    break;
                }
            }
            if (!expanded) frontier.RemoveAt(fi);
        }

        // ── 2. RoomInfo avec portes selon adjacence réelle ───────────────────
        foreach (var (x, y) in placed)
        {
            grid[x, y] = new RoomInfo
            {
                x = x, y = y, type = RoomType.Normal,
                openNorth = placed.Contains((x, y - 1)),
                openSouth = placed.Contains((x, y + 1)),
                openEast  = placed.Contains((x + 1, y)),
                openWest  = placed.Contains((x - 1, y)),
            };
        }

        // ── 3. Boss : dead-end aléatoire ─────────────────────────────────────
        var deadEnds = new List<(int, int)>();
        foreach (var (x, y) in placed)
        {
            int n = 0;
            if (placed.Contains((x, y - 1))) n++;
            if (placed.Contains((x, y + 1))) n++;
            if (placed.Contains((x + 1, y))) n++;
            if (placed.Contains((x - 1, y))) n++;
            if (n == 1) deadEnds.Add((x, y));
        }
        // Fallback : si aucun dead-end (placed très petit), prend une salle quelconque
        if (deadEnds.Count == 0)
            foreach (var p in placed) { deadEnds.Add(p); break; }

        for (int i = deadEnds.Count - 1; i > 0; i--)
        { int j = rng.Next(i + 1); (deadEnds[i], deadEnds[j]) = (deadEnds[j], deadEnds[i]); }

        var (bossX, bossY) = deadEnds[0];
        grid[bossX, bossY].type = RoomType.Boss;

        // ── 4. Spawn : BFS depuis boss, distance >= targetRoomCount / 5 ──────
        int minDist   = Mathf.Max(1, targetRoomCount / 5);
        var distances = BfsDistances(placed, (bossX, bossY));

        var candidates = new List<(int, int)>();
        foreach (var ((cx, cy), d) in distances)
            if (d >= minDist && grid[cx, cy].type != RoomType.Boss)
                candidates.Add((cx, cy));

        if (candidates.Count == 0)
        {
            int maxD = 0;
            foreach (var (_, d) in distances) if (d > maxD) maxD = d;
            foreach (var ((cx, cy), d) in distances) if (d == maxD) candidates.Add((cx, cy));
        }
        for (int i = candidates.Count - 1; i > 0; i--)
        { int j = rng.Next(i + 1); (candidates[i], candidates[j]) = (candidates[j], candidates[i]); }

        var chosen = candidates[rng.Next(candidates.Count)];
        spawnCell  = new Vector2Int(chosen.Item1, chosen.Item2);

        BuildDungeon();

        if (IsServer)
            TeleportPlayersToSpawnClientRpc();
    }

    private static Dictionary<(int, int), int> BfsDistances(HashSet<(int, int)> placed, (int, int) start)
    {
        var dist  = new Dictionary<(int, int), int> { [start] = 0 };
        var queue = new Queue<(int, int)>();
        queue.Enqueue(start);

        (int ddx, int ddy)[] dirs = { (0, -1), (0, 1), (1, 0), (-1, 0) };
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            foreach (var (ddx, ddy) in dirs)
            {
                var next = (x + ddx, y + ddy);
                if (placed.Contains(next) && !dist.ContainsKey(next))
                {
                    dist[next] = dist[(x, y)] + 1;
                    queue.Enqueue(next);
                }
            }
        }
        return dist;
    }

    [ClientRpc]
    private void TeleportPlayersToSpawnClientRpc()
    {
        foreach (var player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (!player.IsOwner) continue;
            player.transform.position = SpawnPosition;
        }
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
        SyncRoomEnteredClientRpc(x, y);

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
            if (bossAnimatorController != null)
                go.GetComponent<Animator>().runtimeAnimatorController = bossAnimatorController;
            var ec = go.GetComponent<EnemyController>();
            ec.SetRoom(x, y);
            ec.isBoss = true;
            ec.SetStats(200f, 25f);
            go.GetComponent<NetworkObject>().Spawn();
        }
        else
        {
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

        int count = Random.Range(minEnemiesPerRoom, maxEnemiesPerRoom + 1);
        roomEnemyCounts[(x, y)] = count;

        for (int i = 0; i < count; i++)
        {
            yield return new WaitForSeconds(enemySpawnInterval);

            Vector3 origin = spawnPoints[Random.Range(0, spawnPoints.Count)];
            Vector2 offset = Random.insideUnitCircle * 0.6f;
            Vector3 pos    = origin + new Vector3(offset.x, offset.y, 0f);

            var go = Instantiate(enemyPrefab, pos, Quaternion.identity);
            var ec = go.GetComponent<EnemyController>();
            ec.SetRoom(x, y);

            float hpMult     = Random.Range(0.7f,  2.0f);
            float speedMult  = Random.Range(0.8f,  2.5f);
            float damageMult = Random.Range(0.8f,  1.8f);
            float scaleMult  = Random.Range(0.6f,  1.6f);
            ec.SetRandomStats(hpMult, speedMult, damageMult, scaleMult);

            go.GetComponent<NetworkObject>().Spawn();
        }
    }

    // Appelé par EnemyController quand un ennemi meurt (server only)
    public void NotifyEnemyDied(int x, int y, Vector3 deathPosition)
    {
        var key = (x, y);
        if (!roomEnemyCounts.ContainsKey(key)) return;
        roomEnemyCounts[key]--;
        if (roomEnemyCounts[key] > 0) return;

        roomEnemyCounts.Remove(key); // évite les double-triggers si des ennemis meurent après le clear
        roomCleared.Add(key);
        SyncRoomClearedClientRpc(x, y);

        bool isBoss = grid[x, y] != null && grid[x, y].type == RoomType.Boss;
        if (isBoss)
        {
            ShowLevelCompleteClientRpc();
        }
        else if (rewardBubblePrefab != null)
        {
            var bubbleGo = Instantiate(rewardBubblePrefab, deathPosition, Quaternion.identity);
            bubbleGo.GetComponent<NetworkObject>().Spawn();
        }

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

    public bool IsRoomBoss(int x, int y) => grid != null && grid[x, y] != null && grid[x, y].type == RoomType.Boss;

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

    [ClientRpc]
    private void SyncRoomEnteredClientRpc(int x, int y)
        => roomEntered.Add((x, y));

    // Synchronise l'état cleared vers tous les clients
    [ClientRpc]
    private void SyncRoomClearedClientRpc(int x, int y)
    {
        roomCleared.Add((x, y));
        if (SoundManager.Instance != null) SoundManager.Instance.StopFightMusic();
    }

    [ClientRpc]
    private void ShowLevelCompleteClientRpc()
    {
        if (levelCompletePrefab != null)
            Instantiate(levelCompletePrefab);
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
        chestRooms.Remove((roomX, roomY));
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
