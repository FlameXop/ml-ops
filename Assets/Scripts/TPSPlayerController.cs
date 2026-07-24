using UnityEngine;
using UnityEngine.UI; // REQUIRED FOR HEALTH BARS
using System.Collections;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(LineRenderer))]
public class TPSPlayerController : MonoBehaviour
{
    [Header("Health System")]
    public float maxHealth = 200f;
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

    // --- NEW: Audio Variables for Player Shooting ---
    [Header("Polish (SFX)")]
    public AudioClip shootSound; 
    public AudioSource playerAudioSource; 

    private CharacterController controller;
    private Vector3 velocity;
    private LineRenderer tracer;
    private float nextFireTime;

    // --- NEW: Added to remember start location ---
    private Vector3 startPosition; 

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

        // --- NEW: Save the exact spot the player spawns in at ---
        startPosition = transform.position; 
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
        
        // Reset the Valorant kill audio back to tier 1
        if (KillManager.Instance != null)
        {
            KillManager.Instance.ResetStreakOnDeath();
        }

        // TODO: Play death animation, restart level, show game over screen
        // anim.SetTrigger("Die");

        // --- NEW: Start the respawn process ---
        StartCoroutine(RespawnRoutine()); 
    }

    // --- NEW: The Respawn Logic ---
    IEnumerator RespawnRoutine()
    {
        // Wait 3 seconds before respawning
        yield return new WaitForSeconds(1f);

        // Turn off controller to allow teleportation
        controller.enabled = false;
        
        // Teleport back to start
        transform.position = startPosition;
        
        // Refill health
        currentHealth = maxHealth;
        if (healthBar != null) healthBar.value = currentHealth;

        // Optional: Reset animator if you have a death animation
        if (anim != null) anim.Play("Idle"); 

        // Turn controller back on so player can move again
        controller.enabled = true;
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

        // --- UPDATED: The Anti-Crash Audio Setup ---
        if (shootSound != null && playerAudioSource != null)
        {
            playerAudioSource.Stop(); 
            playerAudioSource.pitch = Random.Range(0.95f, 1.05f); 
            playerAudioSource.clip = shootSound;
            playerAudioSource.Play();
        }

        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        Vector3 hitPoint;

        // FIX 1: Ignore the Player's own body so you don't block your own bullets
        int layerMask = ~LayerMask.GetMask("Player", "Ignore Raycast");

        // FIX 2: Use QueryTriggerInteraction.Ignore so bullets bypass vision cones/triggers
        if (Physics.Raycast(ray, out RaycastHit hit, weaponRange, layerMask, QueryTriggerInteraction.Ignore))
        {
            hitPoint = hit.point;
            
            if (hit.collider.CompareTag("Enemy"))
            {
                // FIX 3: Use GetComponentInParent so hitting ANY part of the enemy works
                hit.collider.GetComponentInParent<EnemeyGoalAi>()?.TakeDamage(25f, hit.point, hit.normal);            
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