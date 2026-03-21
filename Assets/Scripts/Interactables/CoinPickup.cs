using UnityEngine;

public class CoinPickup : MonoBehaviour
{
    [SerializeField] private float attractRadius = 3f;
    [SerializeField] private float attractSpeed  = 6f;
    [SerializeField] private float collectRadius = 0.4f;

    private Transform target;


    private void Update()
    {
        if (target == null)
        {
            foreach (var player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                if (!player.IsOwner) continue;
                if (Vector2.Distance(transform.position, player.transform.position) <= attractRadius)
                {
                    target = player.transform;
                    break;
                }
            }
        }

        if (target != null)
        {
            transform.position = Vector2.MoveTowards(transform.position, target.position, attractSpeed * Time.deltaTime);

            if (Vector2.Distance(transform.position, target.position) <= collectRadius)
            {
                if (SoundManager.Instance != null) SoundManager.Instance.PlayCoinPickup();
                gameObject.SetActive(false);
            }
        }
    }
}
