using UnityEngine;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(LineRenderer))]
public class PlayerController : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  MOVEMENT
    // ─────────────────────────────────────────────
    [Header("Movement")]
    public float moveSpeed    = 6f;
    public float acceleration = 14f;   // How fast we ramp up to full speed
    public float deceleration = 18f;   // How fast we brake when no input
    public float gravity      = -20f;  // Stronger than real life = snappier shooter feel

    // ─────────────────────────────────────────────
    //  ROTATION
    // ─────────────────────────────────────────────
    [Header("Rotation")]
    public float rotationSpeed = 14f;  // Smoothness of body turning toward aim

    // ─────────────────────────────────────────────
    //  LASER COMBAT
    // ─────────────────────────────────────────────
    [Header("Laser Combat")]
    public Transform firePoint;
    public float fireRate    = 0.15f;
    public float weaponRange = 50f;

    // ─────────────────────────────────────────────
    //  ANIMATION  — drag your Animator here
    //  Your Animator needs ONE float param: "Speed"
    //    0   = idle
    //    > 0 = walking  (set transition threshold to ~0.1)
    // ─────────────────────────────────────────────
    [Header("Animation")]
    public Animator anim;

    // ─────────────────────────────────────────────
    //  PRIVATE STATE
    // ─────────────────────────────────────────────
    private CharacterController controller;
    private Camera              cam;
    private LineRenderer        tracer;

    private Vector3 smoothVelocity;   // Current horizontal velocity (smoothed)
    private float   verticalVelocity; // Separate Y so gravity accumulates properly
    private float   nextFireTime;
    private float   smoothAnimSpeed;  // Damped value fed to the Animator

    // ─────────────────────────────────────────────
    //  INIT
    // ─────────────────────────────────────────────
    void Start()
    {
        controller = GetComponent<CharacterController>();
        tracer     = GetComponent<LineRenderer>();
        cam        = Camera.main;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;

        tracer.positionCount = 2;
        tracer.enabled       = false;
    }

    // ─────────────────────────────────────────────
    //  MAIN LOOP
    // ─────────────────────────────────────────────
    void Update()
    {
        HandleMovement();
        HandleRotation();

        if (Input.GetMouseButton(0) && Time.time >= nextFireTime)
            Shoot();
    }

    // ─────────────────────────────────────────────
    //  MOVEMENT  (camera-relative + smooth accel)
    // ─────────────────────────────────────────────
    void HandleMovement()
    {
        // Raw digital input (-1, 0, 1) — we do our own smoothing below
        float inputX = Input.GetAxisRaw("Horizontal");
        float inputZ = Input.GetAxisRaw("Vertical");

        // Project camera axes onto the ground plane so moving forward
        // always means "where the camera points", not world +Z
        Vector3 camForward = cam.transform.forward;
        Vector3 camRight   = cam.transform.right;
        camForward.y = 0f;
        camRight.y   = 0f;
        camForward.Normalize();
        camRight.Normalize();

        // The direction the player WANTS to move this frame
        Vector3 desiredDirection = (camForward * inputZ + camRight * inputX).normalized;
        Vector3 desiredVelocity  = desiredDirection * moveSpeed;

        // Accelerate toward desired, or decelerate toward zero
        float rate = desiredDirection.magnitude > 0.1f ? acceleration : deceleration;
        smoothVelocity = Vector3.MoveTowards(smoothVelocity, desiredVelocity, rate * Time.deltaTime);

        // ── Gravity ──────────────────────────────
        // Tiny constant negative keeps us stuck to ramps; we don't free-fall
        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -3f;
        else
            verticalVelocity += gravity * Time.deltaTime;

        // Combine horizontal + vertical and move
        Vector3 finalMove = smoothVelocity;
        finalMove.y = verticalVelocity;
        controller.Move(finalMove * Time.deltaTime);

        // ── Drive Walk Animation ──────────────────
        // "Speed" goes 0→1 based on how fast we're actually moving.
        // Smooth it so blend transitions don't pop.
        if (anim != null)
        {
            float targetSpeed = smoothVelocity.magnitude / moveSpeed; // normalised 0..1
            // Exponential smoothing — frame-rate independent
            smoothAnimSpeed = Mathf.Lerp(smoothAnimSpeed, targetSpeed,
                                         1f - Mathf.Exp(-12f * Time.deltaTime));
            anim.SetFloat("Speed", smoothAnimSpeed);
        }
    }

    // ─────────────────────────────────────────────
    //  ROTATION  (smooth Slerp toward crosshair aim)
    // ─────────────────────────────────────────────
    void HandleRotation()
    {
        // Cast a ray from the exact center of the screen (crosshair)
        // so the character always faces where you're about to shoot
        Ray aimRay = cam.ScreenPointToRay(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

        Vector3 aimTarget;
        if (Physics.Raycast(aimRay, out RaycastHit hit, weaponRange + 20f))
            aimTarget = hit.point;
        else
            aimTarget = aimRay.origin + aimRay.direction * (weaponRange + 20f);

        // Flatten so the character never tilts up/down
        Vector3 lookDirection = aimTarget - transform.position;
        lookDirection.y = 0f;

        if (lookDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            // Frame-rate independent Slerp — feels smooth at any FPS
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                1f - Mathf.Exp(-rotationSpeed * Time.deltaTime)
            );
        }
    }

    // ─────────────────────────────────────────────
    //  SHOOT
    // ─────────────────────────────────────────────
    void Shoot()
    {
        nextFireTime = Time.time + fireRate;

        // Raycast from screen center for pixel-perfect accuracy
        Ray aimRay = cam.ScreenPointToRay(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        Vector3 laserEnd;

        if (Physics.Raycast(aimRay, out RaycastHit hit, weaponRange))
        {
            laserEnd = hit.point;
            // Uncomment when your Health component is ready:
            // hit.collider.GetComponent<Health>()?.TakeDamage(damage);
        }
        else
        {
            laserEnd = aimRay.origin + aimRay.direction * weaponRange;
        }

        StartCoroutine(RenderLaser(firePoint.position, laserEnd));
    }

    // ─────────────────────────────────────────────
    //  LASER VISUAL
    // ─────────────────────────────────────────────
    IEnumerator RenderLaser(Vector3 start, Vector3 end)
    {
        tracer.SetPosition(0, start);
        tracer.SetPosition(1, end);
        tracer.enabled = true;
        yield return new WaitForSeconds(0.04f);
        tracer.enabled = false;
    }
}