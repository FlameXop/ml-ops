using UnityEngine;
using UnityEngine.UI; // REQUIRED FOR HEALTH BARS
using System.Collections;

[RequireComponent(typeof(LineRenderer))]
public class EnemyGoalAiNoNavMesh : MonoBehaviour
{
    public enum AIGoal { Patrol, Attack, TakeCover }
    
    [Header("Current Status")]
    public AIGoal currentGoal = AIGoal.Patrol;

    [Header("Health System")]
    public float maxHealth = 100f;
    private float currentHealth;
    public Slider healthBar; // Drag your EnemyHealthBar UI here!

    [Header("Movement Settings")]
    public float moveSpeed = 3f;
    public float turnSpeed = 5f;

    [Header("Patrol Settings")]
    public Transform[] waypoints;
    private int currentWaypointIndex = 0;

    [Header("Combat & Detection")]
    public float detectionRange = 15f;
    public float loseSightDuration = 5f;
    public LayerMask obstacleMask; 
    public Transform firePoint;
    public float fireRate = 0.5f;
    public int shotsBeforeCover = 3;

    [Header("Cover Settings")]
    public Transform[] coverPoints;
    public float timeToSpendInCover = 3f;

    // Component References
    private LineRenderer tracer;
    private Transform player;

    // Internal State Trackers
    private float nextFireTime;
    private int currentShotCount = 0;
    private float timeSinceLastSawPlayer = 0f;
    private float coverTimer = 0f;
    private bool hasReachedCover = false;
    private bool isPlayerVisible = false;
    private Transform activeCoverPoint;

    void Start()
    {
        tracer = GetComponent<LineRenderer>();
        GameObject pObj = GameObject.FindGameObjectWithTag("Player");
        if (pObj != null) player = pObj.transform;

        tracer.positionCount = 2;
        tracer.enabled = false;

        // Initialize Health
        currentHealth = maxHealth;
        if (healthBar != null) healthBar.value = currentHealth;
    }

    void Update()
    {
        if (player == null || currentHealth <= 0) return; // Stop if dead or player is missing

        UpdateSenses();
        EvaluateGoals();
        ExecuteCurrentGoal();
    }

    // --- HEALTH & REACTIVE AI ---
    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        if (healthBar != null) healthBar.value = currentHealth;

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        // DYNAMIC AI REACTION: Instantly abort and run for cover when hit!
        if (coverPoints.Length > 0 && currentGoal != AIGoal.TakeCover)
        {
            Debug.Log("Enemy hit! Running for cover!");
            currentGoal = AIGoal.TakeCover;
            activeCoverPoint = GetBestCover();
            hasReachedCover = false;
            coverTimer = 0f; 
        }
    }

    void Die()
    {
        Debug.Log("ENEMY IS DEAD!");
        // Disable enemy completely
        this.enabled = false; 
        GetComponent<Collider>().enabled = false; // Stop blocking bullets
        
        // TODO: anim.SetTrigger("Die"); 
        // Destroy(gameObject, 3f); // Optional: Delete body after 3 seconds
    }

    // --- SENSORS ---
    void UpdateSenses()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer <= detectionRange && !Physics.Linecast(transform.position, player.position, obstacleMask))
        {
            isPlayerVisible = true;
            timeSinceLastSawPlayer = 0f; 
        }
        else
        {
            isPlayerVisible = false;
            timeSinceLastSawPlayer += Time.deltaTime;
        }
    }

    // --- BRAIN ---
    void EvaluateGoals()
    {
        // If we are already running for cover because we got hit, don't change goals!
        if (currentGoal == AIGoal.TakeCover && !hasReachedCover) return; 

        if (timeSinceLastSawPlayer > loseSightDuration)
        {
            if (currentGoal != AIGoal.Patrol)
            {
                currentGoal = AIGoal.Patrol;
                currentShotCount = 0; 
            }
            return;
        }

        if (timeSinceLastSawPlayer <= loseSightDuration)
        {
            if (currentShotCount >= shotsBeforeCover && coverPoints.Length > 0)
            {
                if (currentGoal != AIGoal.TakeCover)
                {
                    currentGoal = AIGoal.TakeCover;
                    activeCoverPoint = GetBestCover(); 
                    hasReachedCover = false;
                }
            }
            else if (currentGoal != AIGoal.TakeCover || hasReachedCover && coverTimer >= timeToSpendInCover)
            {
                currentGoal = AIGoal.Attack;
            }
        }
    }

    // --- ACTIONS ---
    void ExecuteCurrentGoal()
    {
        switch (currentGoal)
        {
            case AIGoal.Patrol: DoPatrol(); break;
            case AIGoal.Attack: DoAttack(); break;
            case AIGoal.TakeCover: DoTakeCover(); break;
        }
    }

    void DoPatrol()
    {
        if (waypoints.Length == 0) return;
        Transform targetWP = waypoints[currentWaypointIndex];
        MoveToTarget(targetWP.position);

        if (GetFlatDistance(transform.position, targetWP.position) < 0.5f)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        }
    }

    void DoAttack()
    {
        Vector3 lookDir = player.position - transform.position;
        lookDir.y = 0; 
        if (lookDir.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * turnSpeed);
        }

        if (isPlayerVisible && Time.time >= nextFireTime) Shoot();
    }

    void DoTakeCover()
    {
        if (activeCoverPoint == null) return;

        if (!hasReachedCover)
        {
            MoveToTarget(activeCoverPoint.position);
            if (GetFlatDistance(transform.position, activeCoverPoint.position) < 1f)
            {
                hasReachedCover = true;
                coverTimer = 0f;
            }
        }
        else
        {
            coverTimer += Time.deltaTime;
            
            Vector3 lookDir = player.position - transform.position;
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * turnSpeed);
            }

            if (coverTimer >= timeToSpendInCover)
            {
                currentShotCount = 0;
                currentGoal = AIGoal.Attack;
            }
        }
    }

    // --- MOVEMENT SYSTEM ---
    void MoveToTarget(Vector3 targetPos)
    {
        Vector3 direction = targetPos - transform.position;
        direction.y = 0;

        if (direction.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * turnSpeed);
            transform.position = Vector3.MoveTowards(transform.position, new Vector3(targetPos.x, transform.position.y, targetPos.z), moveSpeed * Time.deltaTime);
        }
    }

    float GetFlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

    Transform GetBestCover()
    {
        Transform bestCover = null;
        float closestDistance = Mathf.Infinity;
        foreach (Transform cover in coverPoints)
        {
            float distance = Vector3.Distance(transform.position, cover.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                bestCover = cover;
            }
        }
        return bestCover;
    }

    void Shoot()
    {
        nextFireTime = Time.time + fireRate;
        currentShotCount++;

        // Aim directly at the player's chest height
        Vector3 aimDirection = (player.position + Vector3.up * 1.5f) - firePoint.position;
        Vector3 laserEnd = firePoint.position + aimDirection.normalized * detectionRange;

        // RAYCAST to hit the player
        if (Physics.Raycast(firePoint.position, aimDirection.normalized, out RaycastHit hit, detectionRange))
        {
            laserEnd = hit.point; // Laser stops exactly where it hits
            
            if (hit.collider.CompareTag("Player"))
            {
                // Deal 50 damage (2 shots to kill 100 HP)
                hit.collider.GetComponent<TPSPlayerController>().TakeDamage(50f);
            }
        }

        StartCoroutine(RenderLaser(firePoint.position, laserEnd));
    }

    IEnumerator RenderLaser(Vector3 start, Vector3 end)
    {
        tracer.SetPosition(0, start);
        tracer.SetPosition(1, end);
        tracer.enabled = true;
        yield return new WaitForSeconds(0.04f);
        tracer.enabled = false;
    }
}