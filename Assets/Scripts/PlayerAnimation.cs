using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        float speed = 0f;
        float blend = 0f;

        if (Input.GetKey(KeyCode.W))
            speed = 1f;
        else if (Input.GetKey(KeyCode.S))
            speed = -1f;

        if (Input.GetKey(KeyCode.A))
            blend = -1f;
        else if (Input.GetKey(KeyCode.D))
            blend = 1f;

        animator.SetFloat("Speed", speed);
        animator.SetFloat("Blend", blend);
    }
}