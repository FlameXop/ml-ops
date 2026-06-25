using UnityEngine;
using UnityEngine.AI;

public class EnemyAnimationController : MonoBehaviour
{
    Animator anim;
    NavMeshAgent agent;

    void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        Vector3 localVelocity =
            transform.InverseTransformDirection(
                agent.velocity);

        float moveX =
            localVelocity.x /
            Mathf.Max(agent.speed, 0.01f);

        float moveY =
            localVelocity.z /
            Mathf.Max(agent.speed, 0.01f);

        anim.SetFloat(
            "MoveX",
            moveX,
            0.1f,
            Time.deltaTime);

        anim.SetFloat(
            "MoveY",
            moveY,
            0.1f,
            Time.deltaTime);
    }

    public void Shoot()
    {
        anim.SetTrigger("Shoot");
    }

    public void Die()
    {
        anim.SetTrigger("Die");
    }

    public void SetCombat(bool value)
    {
        anim.SetBool(
            "HasTarget",
            value);
    }
}