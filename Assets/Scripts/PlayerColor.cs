using Unity.Netcode;
using UnityEngine;

public class PlayerColor : NetworkBehaviour
{
    private NetworkVariable<Color> playerColor = new NetworkVariable<Color>(
        Color.white,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Couleur aléatoire à la connexion
            playerColor.Value = Random.ColorHSV(0f, 1f, 0.8f, 1f, 0.8f, 1f);
        }

        // Applique la couleur immédiatement + écoute les changements
        GetComponent<SpriteRenderer>().color = playerColor.Value;
        playerColor.OnValueChanged += OnColorChanged;
    }

    private void OnColorChanged(Color old, Color next)
    {
        GetComponent<SpriteRenderer>().color = next;
    }

    public override void OnNetworkDespawn()
    {
        playerColor.OnValueChanged -= OnColorChanged;
    }
}