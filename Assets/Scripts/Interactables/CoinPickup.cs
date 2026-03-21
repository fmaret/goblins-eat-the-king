using UnityEngine;

public class CoinPickup : MonoBehaviour
{
    [SerializeField] private float attractRadius = 3f;
    [SerializeField] private float attractSpeed  = 6f;

    private Transform target;

    private void Update()
    {
        // Cherche le joueur local le plus proche dans le rayon d'aspiration
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

        // Aspiration vers le joueur
        if (target != null)
            transform.position = Vector2.MoveTowards(transform.position, target.position, attractSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>() == null) return;
        gameObject.SetActive(false);
    }
}
