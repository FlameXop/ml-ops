using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(LineRenderer))]
public class EnemyAI : MonoBehaviour
{
    public enum AIState
    {
        Patrol,
        Investigate,
        Attack,
        Cover,
        Dead
    }

    [Header("State")]
    public AIState currentState;

    [Header("References")]
    public Transform eyes;
    public Transform firePoint;
    public Slider healthBar;

    [Header("Patrol")]
    public Transform[] patrolPoints;

    [Header("Cover")]
    public Transform[] coverPoints;
    public float coverTime = 3f;

    [Header("Vision")]
    public float visionRange = 30f;

    [Range(1, 180)]
    public float visionAngle = 120f;

    public LayerMask obstacleMask;

    [Header("Combat")]
    public float fireRate = 0.5f;
    public float damage = 25f;
    public float strafeRadius = 5f;
    public int shotsBeforeCover = 4;

    [Header("Movement")]
    public float patrolSpeed = 3.5f;
    public float combatSpeed = 5.5f;
    public float coverSpeed = 7f;

    [Header("Health")]
    public float maxHealth = 100f;

    float currentHealth;

    NavMeshAgent agent;
    Animator anim;
    LineRenderer tracer;
    

    Transform player;

    bool playerVisible;

    float nextFireTime;
    float nextStrafeTime;
    float coverTimer;

    int shotCounter;

    Vector3 lastKnownPosition;
    Transform activeCover;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();
        tracer = GetComponent<LineRenderer>();

        tracer.positionCount = 2;
        tracer.enabled = false;

        GameObject p =
            GameObject.FindGameObjectWithTag("Player");

        if (p != null)
            player = p.transform;

        currentHealth = maxHealth;

        if (healthBar != null)
        {
            healthBar.maxValue = maxHealth;
            healthBar.value = maxHealth;
        }

        agent.speed = patrolSpeed;

        currentState = AIState.Patrol;

        PickRandomPatrol();
    }

    void Update()
    {
        if (currentState == AIState.Dead)
            return;

        if (player == null)
            return;

        UpdateVision();

        UpdateAnimator();

        switch (currentState)
        {
            case AIState.Patrol:
                Patrol();
                break;

            case AIState.Investigate:
                Investigate();
                break;

            case AIState.Attack:
                Attack();
                break;

            case AIState.Cover:
                Cover();
                break;
        }
    }

    void UpdateAnimator()
    {
        if (anim == null)
            return;

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

        anim.SetBool(
            "Shoot",
            playerVisible);
    }

    #region VISION

    void UpdateVision()
    {
        playerVisible = CanSeePlayer();

        if (playerVisible)
        {
            lastKnownPosition =
                player.position;
        }
    }

    bool CanSeePlayer()
    {
        Vector3 dir =
            player.position - eyes.position;

        float distance =
            dir.magnitude;

        if (distance > visionRange)
            return false;

        float angle =
            Vector3.Angle(
                transform.forward,
                dir);

        if (angle > visionAngle * 0.5f)
            return false;

        Vector3 head =
            player.position + Vector3.up * 1.7f;

        Vector3 chest =
            player.position + Vector3.up;

        Vector3 legs =
            player.position + Vector3.up * 0.3f;

        return
            CanSeePoint(head) ||
            CanSeePoint(chest) ||
            CanSeePoint(legs);
    }

    bool CanSeePoint(Vector3 point)
    {
        Vector3 dir =
            point - eyes.position;

        float distance =
            dir.magnitude;

        if (Physics.Raycast(
            eyes.position,
            dir.normalized,
            out RaycastHit hit,
            distance))
        {
            return hit.collider.CompareTag("Player");
        }

        return false;
    }

    #endregion

    #region PATROL

    void Patrol()
    {
        agent.speed = patrolSpeed;

        if (playerVisible)
        {
            currentState =
                AIState.Attack;

            return;
        }

        if (!agent.pathPending &&
            agent.remainingDistance < 1f)
        {
            PickRandomPatrol();
        }
    }

    void PickRandomPatrol()
    {
        if (patrolPoints.Length == 0)
            return;

        int index =
            Random.Range(
                0,
                patrolPoints.Length);

        agent.SetDestination(
            patrolPoints[index].position);
    }

    #endregion

    #region INVESTIGATE

    void Investigate()
    {
        if (playerVisible)
        {
            currentState =
                AIState.Attack;

            return;
        }

        if (!agent.pathPending &&
            agent.remainingDistance < 1f)
        {
            currentState =
                AIState.Patrol;

            PickRandomPatrol();
        }
    }

    #endregion

    #region ATTACK

    void Attack()
    {
        agent.speed = combatSpeed;

        if (!playerVisible)
        {
            currentState =
                AIState.Investigate;

            agent.SetDestination(
                lastKnownPosition);

            return;
        }

        FacePlayer();

        if (Time.time > nextStrafeTime)
        {
            RandomStrafe();

            nextStrafeTime =
                Time.time +
                Random.Range(1f, 3f);
        }

        if (Time.time > nextFireTime)
        {
            Shoot();
        }

        if (shotCounter >= shotsBeforeCover)
        {
            FindCover();
        }
    }

    void RandomStrafe()
    {
        Vector3 offset =
            Random.insideUnitSphere *
            strafeRadius;

        offset.y = 0;

        agent.SetDestination(
            player.position +
            offset);
    }

    #endregion

    #region COVER

    void FindCover()
    {
        activeCover = GetBestCover();

        if (activeCover == null)
            return;

        currentState =
            AIState.Cover;

        agent.speed = coverSpeed;

        agent.SetDestination(
            activeCover.position);

        coverTimer = 0f;
    }

    void Cover()
    {
        if (playerVisible)
            FacePlayer();

        if (agent.remainingDistance > 1f)
            return;

        coverTimer += Time.deltaTime;

        if (coverTimer >= coverTime)
        {
            shotCounter = 0;

            currentState =
                AIState.Attack;
        }
    }

    Transform GetBestCover()
    {
        foreach (Transform cover
            in coverPoints)
        {
            if (Physics.Linecast(
                cover.position,
                player.position,
                obstacleMask))
            {
                return cover;
            }
        }

        return null;
    }

    #endregion

    #region SHOOTING

    void Shoot()
    {
        nextFireTime =
            Time.time + fireRate;

        shotCounter++;

        if (anim != null)
            anim.SetTrigger("Shoot");

        Vector3 aim =
            (player.position +
            Vector3.up * 1.5f)
            - firePoint.position;

        Vector3 laserEnd =
            firePoint.position +
            aim.normalized *
            visionRange;

        if (Physics.Raycast(
            firePoint.position,
            aim.normalized,
            out RaycastHit hit,
            visionRange))
        {
            laserEnd = hit.point;

            if (hit.collider.CompareTag("Player"))
            {
                TPSPlayerController p =
                    hit.collider.GetComponent<TPSPlayerController>();

                if (p != null)
                    p.TakeDamage(damage);
            }
        }

        StartCoroutine(
            RenderLaser(
                firePoint.position,
                laserEnd));
    }

    IEnumerator RenderLaser(
        Vector3 start,
        Vector3 end)
    {
        tracer.enabled = true;

        tracer.SetPosition(0, start);
        tracer.SetPosition(1, end);

        yield return new WaitForSeconds(0.05f);

        tracer.enabled = false;
    }

    #endregion

    void FacePlayer()
    {
        Vector3 dir =
            player.position -
            transform.position;

        dir.y = 0;

        Quaternion rot =
            Quaternion.LookRotation(dir);

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                rot,
                Time.deltaTime * 10f);
    }

    public void TakeDamage(float amount)
    {
        if (currentState == AIState.Dead)
            return;

        currentHealth -= amount;

        if (healthBar != null)
        {
            healthBar.value =
                currentHealth;
        }

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        if (currentState != AIState.Cover)
        {
            FindCover();
        }
    }

    void Die()
    {
        currentState =
            AIState.Dead;

        agent.isStopped = true;

        if (anim != null)
            anim.SetTrigger("Die");

        GetComponent<Collider>().enabled =
            false;

        Destroy(gameObject, 5f);
    }

    void OnDrawGizmosSelected()
    {
        if (eyes == null)
            return;

        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            eyes.position,
            visionRange);

        Vector3 left =
            Quaternion.Euler(
                0,
                -visionAngle / 2,
                0)
            * transform.forward;

        Vector3 right =
            Quaternion.Euler(
                0,
                visionAngle / 2,
                0)
            * transform.forward;

        Gizmos.DrawRay(
            eyes.position,
            left * visionRange);

        Gizmos.DrawRay(
            eyes.position,
            right * visionRange);
    }
}