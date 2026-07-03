using UnityEngine;
using UnityEngine.AI;

public class EnemyAnimationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator anim;
    [SerializeField] private NavMeshAgent agent;

    private void Awake()
    {
        if (anim == null)
            anim = GetComponentInChildren<Animator>();

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();
    }

    private void Update()
    {
        UpdateMovement();
    }

    private void UpdateMovement()
    {
        Vector3 localVelocity = transform.InverseTransformDirection(agent.velocity);

        float moveX = localVelocity.x / Mathf.Max(agent.speed, 0.01f);
        float moveY = localVelocity.z / Mathf.Max(agent.speed, 0.01f);
        float speed = agent.velocity.magnitude;

        anim.SetFloat("MoveX", moveX, 0.1f, Time.deltaTime);
        anim.SetFloat("MoveY", moveY, 0.1f, Time.deltaTime);
        anim.SetFloat("Speed", speed, 0.1f, Time.deltaTime);

        anim.SetBool("IsRunning", speed > agent.speed * 0.75f);
    }

    /// <summary>
    /// Called when the enemy spots or loses the player.
    /// </summary>
    public void SetCombat(bool inCombat)
    {
        anim.SetBool("HasTarget", inCombat);

        if (inCombat)
        {
            anim.SetBool("IsInvestigating", false);
            anim.SetBool("IsAiming", true);
        }
        else
        {
            anim.SetBool("IsAiming", false);
        }
    }

    /// <summary>
    /// Called when investigating a sound or last known position.
    /// </summary>
    public void SetInvestigating(bool investigating)
    {
        anim.SetBool("IsInvestigating", investigating);

        if (investigating)
        {
            anim.SetBool("HasTarget", false);
            anim.SetBool("IsAiming", false);
        }
    }

    public void SetAiming(bool aiming)
    {
        anim.SetBool("IsAiming", aiming);
    }

    public void Shoot()
    {
        // Don't play the shoot animation unless we're actually in combat.
        if (!anim.GetBool("HasTarget"))
            return;

        if (!anim.GetBool("IsAiming"))
            return;

        anim.SetTrigger("Shoot");
    }

    public void Die()
    {
        anim.SetTrigger("Die");
    }
}