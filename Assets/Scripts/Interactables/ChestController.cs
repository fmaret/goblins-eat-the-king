using System.Collections;
using UnityEngine;

// À mettre sur le prefab de coffre.
// S'ouvre au contact du joueur. Loot des pièces.
public class ChestController : MonoBehaviour
{
    [SerializeField] private Sprite openSprite;
    [SerializeField] private GameObject coinPrefab;
    [SerializeField] private int coinCount = 5;

    private int roomX, roomY;
    private bool opened;


    public void Init(int x, int y) { roomX = x; roomY = y; }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (opened) return;
        if (other.GetComponent<PlayerController>() == null) return;
        DungeonGenerator.Instance.OpenChestServerRpc(roomX, roomY);
    }

    // Appelé par ClientRpc sur tous les clients
    public void Open()
    {
        if (opened) return;
        opened = true;
        if (SoundManager.Instance != null) SoundManager.Instance.PlayChestOpen();

        var sr = GetComponent<SpriteRenderer>();
        if (openSprite != null && sr != null)
            sr.sprite = openSprite;

        for (int i = 0; i < coinCount; i++)
        {
            if (coinPrefab == null) break;
            Vector2 offset = new Vector2(
                Random.Range(-0.7f, 0.7f),
                Random.Range(-0.7f, 0.7f));
            Instantiate(coinPrefab, transform.position + (Vector3)(Vector2)offset, Quaternion.identity);
        }

        StartCoroutine(HideAfterDelay());
    }

    private static readonly WaitForSeconds hideDelay = new(2f);

    private IEnumerator HideAfterDelay()
    {
        yield return hideDelay;
        gameObject.SetActive(false);
    }
}
