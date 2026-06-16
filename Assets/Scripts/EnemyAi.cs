using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(LineRenderer))]
public class EnemyPatrolAi : MonoBehaviour
{
    [Header("Patrol Settings")]
    public float patrolRadius = 20f;
    private NavMeshAgent agent;

    [Header("Combat & Detection")]
    public float detectionRange = 15f;
    public Transform firePoint;
    public float fireRate = 0.5f;
    
    private Transform player;
    private LineRenderer tracer;
    private float nextFireTime;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        tracer = GetComponent<LineRenderer>();
        
        GameObject pObj = GameObject.FindGameObjectWithTag("Player");
        if (pObj != null) player = pObj.transform;

        tracer.positionCount = 2;
        tracer.enabled = false;

        GoToRandomPatrolPoint();
    }

    void Update()
    {
        if (player == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer <= detectionRange)
        {
            // Player in range: Stop and Shoot
            agent.ResetPath();
            
            Vector3 lookPos = new Vector3(player.position.x, transform.position.y, player.position.z);
            transform.LookAt(lookPos);

            if (Time.time >= nextFireTime) Shoot();

            // TODO: anim.SetBool("isWalking", false);
        }
        else
        {
            // Player out of range: Keep Patrolling
            if (!agent.pathPending && agent.remainingDistance < 0.5f)
            {
                GoToRandomPatrolPoint();
            }

            // TODO: anim.SetBool("isWalking", agent.velocity.magnitude > 0.1f);
        }
    }

    void GoToRandomPatrolPoint()
    {
        Vector3 randomDir = Random.insideUnitSphere * patrolRadius;
        randomDir += transform.position;
        
        // Find the nearest valid ground on the NavMesh
        if (NavMesh.SamplePosition(randomDir, out NavMeshHit hit, patrolRadius, 1))
        {
            agent.SetDestination(hit.position);
        }
    }

    void Shoot()
    {
        nextFireTime = Time.time + fireRate;
        Vector3 laserEnd = firePoint.position + firePoint.forward * detectionRange;

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