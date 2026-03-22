using System.Collections.Generic;
using UnityEngine;

// Composant à mettre sur le prefab de salle.
// Appelé par RoomBuilder pour placer les éléments de décor et interactables.
public class RoomFurnisher : MonoBehaviour
{
    [Header("Décor")]
    [SerializeField] private GameObject pillarPrefab;
    [SerializeField] private GameObject benchPrefab;

    [Header("Interactables")]
    [SerializeField] private GameObject potPrefab;
    [SerializeField] private GameObject chestPrefab;

    private readonly List<PotController> pots = new();
    private ChestController chest;

    public void Furnish(RoomInfo info, float roomSize, int dungeonSeed)
    {
        // RNG déterministe : même résultat sur tous les clients, change à chaque partie
        var rng = new System.Random(dungeonSeed ^ (info.x * 7919 + info.y * 6271));
        float half  = roomSize * 0.45f;
        float inner = half * 0.55f;

        PlacePillars(half);
        PlacePots(info, rng, inner);
        PlaceChest(info, rng, inner);
        PlaceBench(rng, inner);
    }

    // --- Placement ---

    private void PlacePillars(float half)
    {
        if (pillarPrefab == null) return;
        float p = half - 0.8f;
        Spawn(pillarPrefab, new Vector2(-p,  p));
        Spawn(pillarPrefab, new Vector2( p,  p));
        Spawn(pillarPrefab, new Vector2(-p, -p));
        Spawn(pillarPrefab, new Vector2( p, -p));
    }

    private void PlacePots(RoomInfo info, System.Random rng, float range)
    {
        int count = rng.Next(1, 4);
        for (int i = 0; i < count; i++)
        {
            var go = Spawn(potPrefab, RndPos(rng, range));
            if (go == null) continue;
            var pot = go.GetComponent<PotController>();
            if (pot != null) { pot.Init(info.x, info.y, i); pots.Add(pot); }
        }
    }

    private void PlaceChest(RoomInfo info, System.Random rng, float range)
    {
        if (rng.Next(0, 3) != 0) return; // 33% de chance
        var go = Spawn(chestPrefab, RndPos(rng, range));
        if (go == null) return;
        chest = go.GetComponent<ChestController>();
        chest?.Init(info.x, info.y);
        DungeonGenerator.Instance?.RegisterChestRoom(info.x, info.y);
    }

    private void PlaceBench(System.Random rng, float range)
    {
        if (rng.Next(0, 2) == 0) Spawn(benchPrefab, RndPos(rng, range));
    }

    // --- API appelée par DungeonGenerator via ClientRpc ---

    public void BreakPot(int index)
    {
        if (index >= 0 && index < pots.Count)
            pots[index]?.Break();
    }

    public void OpenChest() => chest?.Open();

    // --- Helpers ---

    private GameObject Spawn(GameObject prefab, Vector2 offset)
    {
        if (prefab == null) return null;
        return Instantiate(prefab, transform.position + (Vector3)(Vector2)offset, Quaternion.identity, transform);
    }

    private static Vector2 RndPos(System.Random rng, float range)
        => new Vector2(
            (float)(rng.NextDouble() * 2 - 1) * range,
            (float)(rng.NextDouble() * 2 - 1) * range);
}
