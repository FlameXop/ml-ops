using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("Drag your Player GameObject here")]
    public Transform target; 

    [Header("Camera Feel")]
    [Tooltip("How far away and at what angle the camera sits")]
    public Vector3 offset = new Vector3(0f, 10f, -5f); 
    
    [Tooltip("Lower numbers = smoother/slower. Higher numbers = snappier.")]
    public float smoothSpeed = 5f;

    void LateUpdate()
    {
        if (target == null) return;

        // Calculate where the camera SHOULD be
        Vector3 desiredPosition = target.position + offset;

        // Smoothly glide the camera from its current position to the desired position
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
    }
}