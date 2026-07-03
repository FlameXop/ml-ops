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
    // --- NEW VARIABLES ---
    [Tooltip("If the nearest cover is further than this, the AI might choose to fight instead of run.")]
    public float maxRunDistance = 3.5f;

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
    [Header("Polish (VFX & SFX)")]
    public GameObject hitParticlePrefab; // Assign a spark/fire particle prefab here
    public AudioClip footstepSound;
    public AudioClip shootSound;
    public AudioSource enemyAudioSource;
    
    private Vector3 startPosition;

    void Start()
    {
        startPosition = transform.position;
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
        agent.acceleration = 20f;

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

        // 1. IS THERE ANY COVER AT ALL?
        if (activeCover == null)
        {
            // Nowhere to hide. Choose violence!
            shotCounter = 0; // Reset ammo so he doesn't immediately try to find cover again
            currentState = AIState.Attack;
            return;
        }

        // 2. THE BRAIN: IS IT A SUICIDE RUN?
        // Calculate exactly how far the AI has to run to get to safety
        float distanceToCover = Vector3.Distance(transform.position, activeCover.position);

        // If the cover is really far away AND the player has a clear line of sight...
        if (distanceToCover > maxRunDistance && playerVisible)
        {
            Debug.Log("Cover is too far! Standing my ground!");
            
            // Running is a death sentence. Stand ground and shoot back!
            shotCounter = 0; // Reset shots so he actually fights instead of looping
            currentState = AIState.Attack;
            
            // Optional: You can make him strafe more aggressively here!
            nextStrafeTime = Time.time; 
            
            return; // Stop the code here so he doesn't run.
        }

        // 3. FLIGHT: WE CAN MAKE IT!
        currentState = AIState.Cover;
        agent.speed = coverSpeed;
        agent.SetDestination(activeCover.position);
        
        coverTimer = 0f;
        FacePlayer();

        // Fire a parting shot while turning to run
        if (playerVisible && Time.time >= nextFireTime)
        {
            Shoot();
        }
    }
    void Cover()
    {
        if (agent.remainingDistance > 1f)
            return;

        agent.SetDestination(activeCover.position);
        FacePlayer();

        // FIX: Now the enemy actually waits between shots while in cover!
        if (playerVisible && Time.time >= nextFireTime)
        {
            Shoot();
        }

        coverTimer += Time.deltaTime;

        if (coverTimer >= coverTime)
        {
            shotCounter = 0;
            currentState = AIState.Attack;
        }
    }

    Transform GetBestCover()
    {
        Transform bestCover = null;
        float minDistanceToEnemy = Mathf.Infinity;

        // Get the direction from the enemy to the player
        Vector3 dirToPlayer = (player.position - transform.position).normalized;

        foreach (Transform cover in coverPoints)
        {
            // 1. Does this cover break line of sight with the player?
            if (Physics.Linecast(cover.position, player.position, obstacleMask))
            {
                // Get the direction from the enemy to this specific cover
                Vector3 dirToCover = (cover.position - transform.position).normalized;

                // 2. Is the cover away from the player?
                // Vector3.Dot compares the two directions. 
                // A value < 0 means the cover is behind the enemy. 
                // A value of 0.2f gives a little leniency so they can move sideways to cover.
                if (Vector3.Dot(dirToPlayer, dirToCover) < 0.2f)
                {
                    float distanceToCover = Vector3.Distance(transform.position, cover.position);

                    // 3. Is this the closest valid cover we've checked so far?
                    if (distanceToCover < minDistanceToEnemy)
                    {
                        minDistanceToEnemy = distanceToCover;
                        bestCover = cover;
                    }
                }
            }
        }

        // Fallback: If no "ideal" cover away from the player is found, 
        // just find the nearest cover that at least breaks line of sight.
        if (bestCover == null)
        {
            foreach (Transform cover in coverPoints)
            {
                if (Physics.Linecast(cover.position, player.position, obstacleMask))
                {
                    float dist = Vector3.Distance(transform.position, cover.position);
                    if (dist < minDistanceToEnemy)
                    {
                        minDistanceToEnemy = dist;
                        bestCover = cover;
                    }
                }
            }
        }

        return bestCover;
    }
    #endregion

    #region SHOOTING

 void Shoot()
    {
        nextFireTime = Time.time + fireRate;
        shotCounter++;

        if (anim != null) anim.SetTrigger("Shoot");

        // NEW: Play the bullet sound every time the gun fires!
        if (shootSound != null && enemyAudioSource != null)
        {
            enemyAudioSource.PlayOneShot(shootSound);
        }

        Vector3 aim = (player.position + Vector3.up * 1.5f) - firePoint.position;
        Vector3 laserEnd = firePoint.position + aim.normalized * visionRange;

        if (Physics.Raycast(firePoint.position, aim.normalized, out RaycastHit hit, visionRange))
        {
            laserEnd = hit.point;

            if (hit.collider.CompareTag("Player"))
            {
                TPSPlayerController p = hit.collider.GetComponent<TPSPlayerController>();
                if (p != null) p.TakeDamage(damage);
            }
        }

        StartCoroutine(RenderLaser(firePoint.position, laserEnd));
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

   public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (currentState == AIState.Dead) return;

        currentHealth -= amount;

        // Spawn hit particle facing away from the wall/enemy
        if (hitParticlePrefab != null)
        {
            Instantiate(hitParticlePrefab, hitPoint, Quaternion.LookRotation(hitNormal));
        }

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        if (currentState != AIState.Cover) FindCover();
    }

    void Die()
    {
        currentState = AIState.Dead;
        agent.isStopped = true;
        
        if (anim != null) anim.SetTrigger("Die");
        GetComponent<Collider>().enabled = false;

        // Trigger the Valorant kill sound
        if (KillManager.Instance != null)
        {
            KillManager.Instance.RegisterKill();
        }

        // Start the respawn loop instead of Destroy(gameObject)
        StartCoroutine(RespawnRoutine());
    }

   System.Collections.IEnumerator RespawnRoutine()
    {
        // Wait for the death animation to finish and clear the body
        yield return new WaitForSeconds(1.5f);
        
        // Hide enemy temporarily, BUT skip the tracer!
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (r != tracer) r.enabled = false; 
        }

        // Forcefully shut the laser off so it doesn't get stuck in the air
        tracer.enabled = false;

        yield return new WaitForSeconds(0.5f); // Wait 2 seconds before respawning

        // Reset everything
        transform.position = startPosition;
        currentHealth = maxHealth;
        GetComponent<Collider>().enabled = true;
        
        // Turn the body back on, BUT leave the tracer turned off!
        foreach (var r in renderers)
        {
            if (r != tracer) r.enabled = true; 
        }
        
        if (anim != null) anim.Play("Idle"); // Reset animator
        
        // Push the fire timer 1.5 seconds into the future so they don't instakill you
        nextFireTime = Time.time + fireRate + 0.4f;
        shotCounter = 0;
        
        agent.isStopped = false;
        currentState = AIState.Patrol;
        PickRandomPatrol();
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