using UnityEngine;

// À mettre sur le prefab de pot.
// Cassable par l'épée du joueur. Loot des pièces à la casse.
public class PotController : MonoBehaviour
{
    [SerializeField] private GameObject coinPrefab;
    [SerializeField] private int coinCount = 3;

    private int roomX, roomY, potIndex;
    private bool broken;


    public void Init(int x, int y, int index)
    {
        roomX    = x;
        roomY    = y;
        potIndex = index;
    }

    // Appelé par SwordHitbox
    public void TakeDamage()
    {
        if (broken) return;
        DungeonGenerator.Instance.BreakPotServerRpc(roomX, roomY, potIndex);
    }

    // Appelé par ClientRpc sur tous les clients
    public void Break()
    {
        if (broken) return;
        broken = true;
        if (SoundManager.Instance != null) SoundManager.Instance.PlayPotBreak();

        // Spawn des pièces localement (même résultat sur tous les clients)
        for (int i = 0; i < coinCount; i++)
        {
            if (coinPrefab == null) break;
            Vector2 offset = new Vector2(
                Random.Range(-0.5f, 0.5f),
                Random.Range(-0.5f, 0.5f));
            Instantiate(coinPrefab, transform.position + (Vector3)(Vector2)offset, Quaternion.identity);
        }

        gameObject.SetActive(false);
    }
}
