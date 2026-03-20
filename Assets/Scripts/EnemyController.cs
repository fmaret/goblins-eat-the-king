using UnityEngine;
using Unity.Netcode;

public class EnemyController : NetworkBehaviour
{
    [SerializeField] private float maxHp = 100f;
    [SerializeField] private StatBar healthBar;

    private NetworkVariable<float> hp = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            hp.Value = maxHp;

        hp.OnValueChanged += OnHpChanged;
        UpdateHealthBar();
    }

    public override void OnNetworkDespawn()
    {
        hp.OnValueChanged -= OnHpChanged;
    }

    // Appelé par SwordHitbox depuis le client owner
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void TakeDamageServerRpc(float damage)
    {
        Debug.Log($"[EnemyController] TakeDamageServerRpc called with damage: {damage}");
        hp.Value = Mathf.Max(0f, hp.Value - damage);

        if (hp.Value <= 0f)
            DieClientRpc();
    }

    [ClientRpc]
    private void DieClientRpc()
    {
        // Lance l'animation de mort si elle existe
        var animator = GetComponent<Animator>();
        if (animator != null)
            animator.SetTrigger("Die");
    }

    // Destruction réelle gérée par le serveur après le délai de l'animation
    public void DestroyEnemy()
    {
        if (IsServer)
            GetComponent<NetworkObject>().Despawn();
    }

    private void OnHpChanged(float oldHp, float newHp)
    {
        UpdateHealthBar();
    }

    private void UpdateHealthBar()
    {
        if (healthBar != null)
            healthBar.Set(hp.Value, maxHp, $"HP : {Mathf.CeilToInt(hp.Value)}/{maxHp}");
    }
}
