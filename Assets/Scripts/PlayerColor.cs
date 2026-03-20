using Unity.Netcode;
using UnityEngine;

public class PlayerColor : NetworkBehaviour
{
    private NetworkVariable<Color> playerColor = new NetworkVariable<Color>(
        Color.white,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    public Color basePlayerColor;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Si aucune couleur assignée dans l'Inspector (alpha = 0), couleur random
            bool hasBaseColor = basePlayerColor.a > 0f;
            playerColor.Value = hasBaseColor
                ? basePlayerColor
                : Random.ColorHSV(0f, 1f, 0.8f, 1f, 0.8f, 1f);
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