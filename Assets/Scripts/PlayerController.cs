using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : NetworkBehaviour
{
    [SerializeField] private float speed = 5f;

    private Rigidbody2D rb;

    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody2D>();

        if (!IsOwner)
        {
            enabled = false;
            return;
        }
    }

    void FixedUpdate()
    {
        Vector2 dir = InputSystem.actions["Move"].ReadValue<Vector2>();        
        rb.linearVelocity = dir * speed;
    }
}