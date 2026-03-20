using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 3f;
    private Rigidbody2D rb;
    private Animator animator;
    private Vector2 movement;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    // ✅ Méthode compatible Unity Input System
    public void OnMove(InputValue value)
    {
        movement = value.Get<Vector2>();
        Debug.Log($"[OnMove] Input reçu : {movement}");
    }

    void Update()
    {
        animator.SetFloat("horizontal", movement.x);
        animator.SetFloat("vertical", movement.y);
        animator.SetBool("isMoving", movement != Vector2.zero);
        Debug.Log($"[Animator] isMoving={movement != Vector2.zero}, h={movement.x}, v={movement.y}");
    }

    void FixedUpdate()
    {
        rb.MovePosition(rb.position + movement * moveSpeed * Time.fixedDeltaTime);
    }
}