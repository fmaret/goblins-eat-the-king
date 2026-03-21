using UnityEngine;

// À mettre sur un GameObject enfant du joueur avec un Collider2D en mode Trigger
// Ce GameObject représente la zone de frappe de l'épée
public class SwordHitbox : MonoBehaviour
{
    [SerializeField] private float damage = 25f;

    private Collider2D hitCollider;

    void Awake()
    {
        hitCollider = GetComponent<Collider2D>();
        hitCollider.enabled = false; // désactivé par défaut
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"[SwordHitbox] Touche : {other.name} (tag={other.tag})");

        var enemy = other.GetComponent<EnemyController>();
        if (enemy != null) { enemy.TakeDamageServerRpc(damage); return; }

        var pot = other.GetComponentInParent<PotController>();
        Debug.Log($"[SwordHitbox] PotController trouvé : {pot != null}");
        if (pot != null) pot.TakeDamage();
    }
}
