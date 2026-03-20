using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float sprintMultiplier = 2f;
    private Rigidbody2D rb;
    private Animator animator;
    private Vector2 movement;
    private bool isSprinting;
    private bool isAttacking;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    // ✅ Méthode compatible Unity Input System
    public void OnMove(InputValue value)
    {
        movement = value.Get<Vector2>();
    }

    public void OnSprint(InputValue value)
    {
        isSprinting = value.isPressed;
    }

    public void OnAttack(InputValue value)
    {
        if (value.isPressed && !isAttacking)
        {
            StartCoroutine(AttackRoutine());
        }
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;
        animator.SetBool("isAttacking", true);

        // Attendre un frame que la transition vers l'attaque démarre
        yield return null;

        // Attendre la fin complète de l'animation d'attaque
        while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
        {
            yield return null;
        }

        isAttacking = false;
        animator.SetBool("isAttacking", false);
    }

    void Update()
    {
        bool isMoving = movement != Vector2.zero;
        animator.SetBool("isMoving", isMoving);
        animator.SetBool("isSprinting", isSprinting && isMoving);

        if (isMoving)
        {
            // Normalisation : évite les valeurs intermédiaires qui font clignoter le blend tree
            Vector2 dir = movement.normalized;
            animator.SetFloat("InputX", dir.x);
            animator.SetFloat("InputY", dir.y);
            // LastInput garde la dernière direction pour l'animation idle
            animator.SetFloat("LastInputX", dir.x);
            animator.SetFloat("LastInputY", dir.y);
            Debug.Log($"[Animator] InputX={dir.x:F2}, InputY={dir.y:F2} | state={animator.GetCurrentAnimatorStateInfo(0).shortNameHash}, isAttacking={isAttacking}, isSprinting={isSprinting}");
        }
    }

    void FixedUpdate()
    {
        if (isAttacking) return; // bloque le mouvement pendant l'attaque
        float currentSpeed = moveSpeed * (isSprinting ? sprintMultiplier : 1f);
        rb.MovePosition(rb.position + movement * currentSpeed * Time.fixedDeltaTime);
    }
}