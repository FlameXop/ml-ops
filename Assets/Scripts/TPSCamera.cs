using UnityEngine;

public class TPSCamera : MonoBehaviour
{
    [Header("Target & Positioning")]
    public Transform followTarget; 
    public Vector3 normalOffset = new Vector3(0f, 0f, -3f);
    public Vector3 aimOffset = new Vector3(0.5f, 0f, -1.5f);
    public float smoothSpeed = 10f;

    [Header("Camera Look Settings")]
    public float mouseSensitivity = 2f;
    public float minPitch = -40f;
    public float maxPitch = 60f;

    [Header("Zoom Settings")]
    public float normalFOV = 60f;
    public float aimFOV = 40f;

    private float yaw = 0f;
    private float pitch = 0f;
    private Camera cam;
    private Vector3 currentOffset;

    void Start()
    {
        cam = GetComponent<Camera>();
        Cursor.lockState = CursorLockMode.Locked; 
        Cursor.visible = false;
        currentOffset = normalOffset;
    }

    void LateUpdate()
    {
        if (followTarget == null) return;

        // 1. Get Mouse Input
        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // 2. Check if Aiming (Right Mouse Button)
        bool isAiming = Input.GetButton("Fire2");

        // 3. Smoothly transition Offset and Field of View
        Vector3 targetOffset = isAiming ? aimOffset : normalOffset;
        currentOffset = Vector3.Lerp(currentOffset, targetOffset, Time.deltaTime * smoothSpeed);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, isAiming ? aimFOV : normalFOV, Time.deltaTime * smoothSpeed);

        // 4. Calculate Camera Rotation & Position
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
        
        // INSTANT snap to target position to prevent jitter, but smooth the local offset
        Vector3 desiredPosition = followTarget.position + rotation * currentOffset;
        transform.position = desiredPosition; 
        
        // Look exactly at the target
        transform.LookAt(followTarget.position); 
    }
}