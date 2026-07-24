using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(LineRenderer))]
public class EnemeyGoalAi : MonoBehaviour
{
    public enum AIState { Patrol, Investigate, Combat, Retreat, Ambush, Dead } // NEW: Ambush State
    public enum CombatTactic { WideSwing, CrouchSpray, JigglePeek, HoldAngle } // NEW: HoldAngle tactic

    [Header("Core State")]
    public AIState currentState;
    public CombatTactic currentTactic;

    [Header("References")]
    public Transform eyes;
    public Transform firePoint;
    public Slider healthBar;

    [Header("Patrol")]
    public Transform[] patrolPoints;

    [Header("Cover & Memory")]
    public Transform[] coverPoints;
    public float fearLevel = 0f; 
    public LayerMask obstacleMask;

    [Header("AAA Gunplay & Headshots")]
    public float fireRate = 0.15f; 
    public float baseDamage = 25f;
    public float headshotMultiplier = 8f; 
    
    [Header("Human-Like Delays")]
    public float reactionTime = 0.35f; 
    private float canShootTime;
    private bool wasPlayerVisibleLastFrame;
    private float aggroTimer = 0f; // NEW: Forces him to stay in combat when shot

    [Header("Movement")]
    public float patrolSpeed = 3.5f;
    public float walkSpeed = 3.5f;
    public float sprintSpeed = 6.5f;
    public float crouchSpeed = 2f;

    [Header("Health")]
    public float maxHealth = 100f;
    private float currentHealth;

    // Components
    NavMeshAgent agent;
    Animator anim;
    LineRenderer tracer;
    Transform player;

    // Tracking
    bool playerVisible;
    float nextFireTime;
    float tacticTimer;
    CombatTactic lastTactic = CombatTactic.CrouchSpray;
    
    Vector3 hidePosition;
    Vector3 peekPosition;
    Vector3 startPosition;
    Vector3 lastKnownPosition;
    bool isCrouching = false;

    [Header("Polish (VFX & SFX)")]
    public GameObject hitParticlePrefab;
    public AudioClip shootSound;
    public AudioSource enemyAudioSource;

    void Start()
    {
        startPosition = transform.position;
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();
        tracer = GetComponent<LineRenderer>();
        tracer.positionCount = 2;
        tracer.enabled = false;

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;

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
        if (currentState == AIState.Dead || player == null) return;

        if (aggroTimer > 0) aggroTimer -= Time.deltaTime;

        UpdateVision();
        UpdateAnimator();

        switch (currentState)
        {
            case AIState.Patrol: HandlePatrol(); break;
            case AIState.Combat: HandleCombat(); break;
            case AIState.Retreat: HandleRetreat(); break;
            case AIState.Investigate: HandleInvestigate(); break;
            case AIState.Ambush: HandleAmbush(); break; // NEW: The rat tactic
        }
    }

    void UpdateAnimator()
    {
        if (anim == null) return;
        Vector3 localVelocity = transform.InverseTransformDirection(agent.velocity);
        
        anim.SetFloat("MoveX", localVelocity.x / Mathf.Max(agent.speed, 0.01f), 0.1f, Time.deltaTime);
        anim.SetFloat("MoveY", localVelocity.z / Mathf.Max(agent.speed, 0.01f), 0.1f, Time.deltaTime);
        
        anim.SetBool("Shoot", playerVisible && currentState == AIState.Combat && !isCrouching);
        anim.SetBool("Crouch", isCrouching);
    }

    void UpdateVision()
    {
        Vector3 targetPoint = player.position + Vector3.up * 1.5f; 
        Vector3 dirToPlayer = targetPoint - eyes.position;
        float distance = dirToPlayer.magnitude;

        int visionMask = ~LayerMask.GetMask("Enemy", "Ignore Raycast");
        playerVisible = false; 

        // FIX: If he was shot recently, he automatically knows where you are (Aggro Override)
        if (aggroTimer > 0)
        {
            playerVisible = true;
            lastKnownPosition = player.position;
        }
        else if (distance <= 40f)
        {
            float angle = Vector3.Angle(transform.forward, dirToPlayer);
            if (angle < 70f || distance < 3f) 
            {
                if (Physics.Raycast(eyes.position, dirToPlayer.normalized, out RaycastHit hit, 40f, visionMask, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.CompareTag("Player"))
                    {
                        playerVisible = true;
                    }
                }
            }
        }

        if (playerVisible && !wasPlayerVisibleLastFrame)
        {
            canShootTime = Time.time + reactionTime;
        }
        wasPlayerVisibleLastFrame = playerVisible;

        if (playerVisible)
        {
            lastKnownPosition = player.position;
            if (currentState == AIState.Patrol || currentState == AIState.Investigate || currentState == AIState.Ambush)
            {
                ChooseNewTactic();
                currentState = AIState.Combat;
            }
        }
    }

    #region PATROL, INVESTIGATE & AMBUSH
    void HandlePatrol()
    {
        isCrouching = false;
        agent.speed = patrolSpeed;
        if (!agent.pathPending && agent.remainingDistance < 1f) PickRandomPatrol();
    }

    void PickRandomPatrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;
        agent.SetDestination(patrolPoints[Random.Range(0, patrolPoints.Length)].position);
    }

    void HandleInvestigate()
    {
        isCrouching = false;
        agent.speed = walkSpeed;
        if (!agent.pathPending && agent.remainingDistance < 1f)
        {
            // Instead of going back to patrol, he gets ratty and sets up an ambush
            currentState = AIState.Ambush;
            Transform cheekyCorner = GetBestCover();
            if (cheekyCorner != null) agent.SetDestination(cheekyCorner.position);
        }
    }

    void HandleAmbush()
    {
        // He runs to a corner, crouches, and waits for you to walk by
        if (agent.remainingDistance < 1f)
        {
            isCrouching = true;
            // Aim at the exact spot he last saw you, waiting for you to peek
            Vector3 lookDir = lastKnownPosition - transform.position;
            lookDir.y = 0;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 5f);
        }
        else
        {
            agent.speed = sprintSpeed;
            isCrouching = false;
        }
    }
    #endregion

    #region AAA COMBAT LOGIC
    void HandleCombat()
    {
        FacePlayer();

        if (currentHealth < maxHealth * 0.4f)
        {
            currentState = AIState.Retreat;
            return;
        }

        // If he loses sight of you mid-fight, he starts investigating
        if (!playerVisible)
        {
            currentState = AIState.Investigate;
            agent.SetDestination(lastKnownPosition);
            return;
        }

        tacticTimer -= Time.deltaTime;
        if (tacticTimer <= 0) ChooseNewTactic();

        switch (currentTactic)
        {
            case CombatTactic.CrouchSpray:
                isCrouching = true;
                agent.isStopped = true; 
                if (Time.time > nextFireTime && Time.time >= canShootTime) Shoot();
                break;

            case CombatTactic.WideSwing:
                isCrouching = false;
                agent.isStopped = false;
                agent.speed = sprintSpeed;
                if (agent.remainingDistance < 1f) 
                {
                    Vector3 strafeDir = transform.right * (Random.value > 0.5f ? 5f : -5f);
                    if (NavMesh.SamplePosition(transform.position + strafeDir, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                    {
                        agent.SetDestination(hit.position);
                    }
                }
                if (Time.time > nextFireTime && Time.time >= canShootTime) Shoot();
                break;

            case CombatTactic.JigglePeek:
                isCrouching = false;
                agent.isStopped = false;
                agent.speed = walkSpeed;
                
                if (tacticTimer % 2f > 1f) 
                {
                    agent.SetDestination(peekPosition);
                    if (Time.time > nextFireTime && Time.time >= canShootTime) Shoot();
                }
                else 
                {
                    agent.SetDestination(hidePosition);
                }
                break;
                
            case CombatTactic.HoldAngle:
                isCrouching = true;
                agent.isStopped = true;
                // Dead accuracy, no movement, just waiting for you to step into his crosshair
                if (Time.time > nextFireTime && Time.time >= canShootTime) Shoot();
                break;
        }
    }

    void ChooseNewTactic()
    {
        CombatTactic newTactic;
        do {
            newTactic = (CombatTactic)Random.Range(0, 4);
        } while (newTactic == lastTactic);

        if (fearLevel > 2f && Random.value > 0.3f) newTactic = CombatTactic.JigglePeek;

        currentTactic = newTactic;
        lastTactic = newTactic;

        if (currentTactic == CombatTactic.CrouchSpray) tacticTimer = 1.5f; 
        if (currentTactic == CombatTactic.WideSwing) tacticTimer = 2.5f; 
        if (currentTactic == CombatTactic.HoldAngle) tacticTimer = 2.0f;
        
        if (currentTactic == CombatTactic.JigglePeek)
        {
            tacticTimer = 4f;
            Transform cover = GetBestCover();
            if (cover != null)
            {
                hidePosition = cover.position;
                peekPosition = hidePosition + (transform.right * 1.5f);
            }
            else currentTactic = CombatTactic.CrouchSpray; 
        }
    }
    #endregion

    #region SURVIVAL & COVER
    void HandleRetreat()
    {
        isCrouching = false;
        agent.isStopped = false;
        agent.speed = sprintSpeed;
        
        Transform safeCover = GetBestCover();
        if (safeCover != null) agent.SetDestination(safeCover.position);

        if (agent.remainingDistance < 1f)
        {
            fearLevel += 1f; 
            isCrouching = true;
            tacticTimer = 3f;
            currentState = AIState.Combat;
            currentTactic = CombatTactic.HoldAngle; // He waits at the cover to ambush you if you chase
        }
    }

    Transform GetBestCover()
    {
        Transform bestCover = null;
        float minDistanceToEnemy = Mathf.Infinity;
        Vector3 dirToPlayer = (player.position - transform.position).normalized;

        if (coverPoints == null || coverPoints.Length == 0) return null;

        foreach (Transform cover in coverPoints)
        {
            if (Physics.Linecast(cover.position, player.position, obstacleMask))
            {
                Vector3 dirToCover = (cover.position - transform.position).normalized;
                if (Vector3.Dot(dirToPlayer, dirToCover) < 0.2f)
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

    #region SHOOTING & DAMAGE
    void Shoot()
    {
        nextFireTime = Time.time + fireRate;
        if (anim != null) anim.SetTrigger("Shoot");

        if (shootSound != null && enemyAudioSource != null)
        {
            enemyAudioSource.pitch = Random.Range(0.95f, 1.05f);
            enemyAudioSource.PlayOneShot(shootSound);
        }

        Vector3 playerHead = player.position + Vector3.up * 1.6f; 
        Vector3 aimDir = (playerHead - eyes.position).normalized;
        Vector3 laserEnd = firePoint.position + aimDir * 40f;

        int shootMask = ~LayerMask.GetMask("Enemy", "Ignore Raycast");

        if (Physics.Raycast(eyes.position, aimDir, out RaycastHit hit, 40f, shootMask, QueryTriggerInteraction.Ignore))
        {
            laserEnd = hit.point;
            
            if (hit.collider.CompareTag("Player"))
            {
                float hitHeight = hit.point.y - hit.collider.bounds.min.y;
                float playerHeight = hit.collider.bounds.size.y;
                
                bool isHeadshot = (hitHeight / playerHeight) > 0.8f;
                float finalDamage = isHeadshot ? baseDamage * headshotMultiplier : baseDamage;

                hit.collider.GetComponent<TPSPlayerController>()?.TakeDamage(finalDamage);
            }
        }

        StartCoroutine(RenderLaser(firePoint.position, laserEnd));
    }

    IEnumerator RenderLaser(Vector3 start, Vector3 end)
    {
        tracer.enabled = true;
        tracer.SetPosition(0, start);
        tracer.SetPosition(1, end);
        yield return new WaitForSeconds(0.04f); 
        tracer.enabled = false;
    }

    void FacePlayer()
    {
        Vector3 dir = player.position - transform.position;
        dir.y = 0;
        if (dir.sqrMagnitude > 0.1f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 15f); // Sped up the turning slightly
        }
    }

    public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (currentState == AIState.Dead) return;
        currentHealth -= amount;
        
        if (hitParticlePrefab != null) Instantiate(hitParticlePrefab, hitPoint, Quaternion.LookRotation(hitNormal));

        if (currentHealth <= 0)
        {
            Die();
            return;
        }
        
        // --- THE FIXED 180 SNAP & AGGRO LOGIC ---
        aggroTimer = 3f; // Forces vision to stay true for 3 seconds even if he looks away
        playerVisible = true;
        lastKnownPosition = player.position;
        currentState = AIState.Combat;

        // 1. Instant 180 Flick Shot to the Head
        Vector3 dirToYou = player.position - transform.position;
        dirToYou.y = 0;
        transform.rotation = Quaternion.LookRotation(dirToYou);

        // 2. React
        if (Random.value > 0.5f)
        {
            currentTactic = CombatTactic.CrouchSpray;
            tacticTimer = 2f; 
            isCrouching = true;
            agent.isStopped = true;
            canShootTime = Time.time; // Instantly pull the trigger because he got shot
        }
        else
        {
            currentTactic = CombatTactic.WideSwing; 
            tacticTimer = 1.5f;
            isCrouching = false;
            agent.isStopped = false;
            
            Vector3 dodgeDir = transform.right * (Random.value > 0.5f ? 4f : -4f);
            if (NavMesh.SamplePosition(transform.position + dodgeDir, out NavMeshHit navHit, 5f, NavMesh.AllAreas))
            {
                agent.SetDestination(navHit.position);
            }
        }
    }

    void Die()
    {
        currentState = AIState.Dead;
        agent.isStopped = true;
        
        if (anim != null) anim.SetTrigger("Die");
        GetComponent<Collider>().enabled = false;

        if (KillManager.Instance != null)
        {
            KillManager.Instance.RegisterKill();
        }

        StartCoroutine(RespawnRoutine());
    }

    IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(1.5f);
        
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers) if (r != tracer) r.enabled = false; 
        tracer.enabled = false;

        yield return new WaitForSeconds(1.5f); 

        transform.position = startPosition;
        currentHealth = maxHealth;
        GetComponent<Collider>().enabled = true;
        
        foreach (var r in renderers) if (r != tracer) r.enabled = true; 
        
        if (anim != null) anim.Play("Idle"); 
        
        nextFireTime = Time.time + fireRate + 0.5f;
        fearLevel = 0f; 
        
        agent.isStopped = false;
        currentState = AIState.Patrol;
        PickRandomPatrol();
    }
    #endregion
}