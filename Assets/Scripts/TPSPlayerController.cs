using UnityEngine;
using UnityEngine.UI; // REQUIRED FOR HEALTH BARS
using System.Collections;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(LineRenderer))]
public class TPSPlayerController : MonoBehaviour
{
    [Header("Health System")]
    public float maxHealth = 100f;
    private float currentHealth;
    public Slider healthBar; // Drag your PlayerHealthBar UI here!

    [Header("Movement")]
    public float walkSpeed = 5f;
    public float gravity = -9.81f;
    
    [Header("Shooting")]
    public Transform firePoint; 
    public float weaponRange = 100f;
    public float fireRate = 0.2f;
    
    [Header("References")]
    public Transform cameraTransform;
    public Animator anim;

    private CharacterController controller;
    private Vector3 velocity;
    private LineRenderer tracer;
    private float nextFireTime;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        tracer = GetComponent<LineRenderer>();
        if (anim == null) anim = GetComponentInChildren<Animator>();

        tracer.positionCount = 2;
        tracer.enabled = false;

        // Initialize Health
        currentHealth = maxHealth;
        if (healthBar != null) healthBar.value = currentHealth;
    }

    void Update()
    {
        if (currentHealth <= 0) return; // Can't move or shoot if dead

        MovePlayer();
        RotatePlayer();
        HandleShooting();
    }

    // --- HEALTH SYSTEM ---
    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        if (healthBar != null) healthBar.value = currentHealth;

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log("PLAYER IS DEAD!");
        // TODO: Play death animation, restart level, show game over screen
        // anim.SetTrigger("Die");
    }

    // --- MOVEMENT & COMBAT ---
    void MovePlayer()
    {
        float horizontal = Input.GetAxis("Horizontal"); 
        float vertical = Input.GetAxis("Vertical");     

        if (anim != null)
        {
            anim.SetFloat("Blend", horizontal);
            anim.SetFloat("Speed", vertical);
        }

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;
        
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection = (forward * vertical + right * horizontal).normalized;

        controller.Move(moveDirection * walkSpeed * Time.deltaTime);

        if (controller.isGrounded && velocity.y < 0) velocity.y = -2f;
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    void RotatePlayer()
    {
        Vector3 lookDir = cameraTransform.forward;
        lookDir.y = 0; 
        if (lookDir.sqrMagnitude > 0.1f) transform.rotation = Quaternion.LookRotation(lookDir);
    }

    void HandleShooting()
    {
        if (Input.GetButton("Fire1") && Time.time >= nextFireTime) Shoot();
    }

    void Shoot()
    {
        nextFireTime = Time.time + fireRate;

        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        Vector3 hitPoint;

        if (Physics.Raycast(ray, out RaycastHit hit, weaponRange))
        {
            hitPoint = hit.point;
            
            // DID WE HIT THE ENEMY?
            if (hit.collider.CompareTag("Enemy"))
            {
                // Deal 25 damage (4 shots to kill 100 HP)
                hit.collider.GetComponent<EnemyAI>().TakeDamage(25f);
            }
        }
        else
        {
            hitPoint = ray.GetPoint(weaponRange);
        }

        if (firePoint != null) StartCoroutine(RenderLaser(firePoint.position, hitPoint));
    }

    IEnumerator RenderLaser(Vector3 start, Vector3 end)
    {
        tracer.SetPosition(0, start);
        tracer.SetPosition(1, end);
        tracer.enabled = true;
        yield return new WaitForSeconds(0.05f);
        tracer.enabled = false;
    }
}