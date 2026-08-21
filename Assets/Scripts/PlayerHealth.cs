using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
public class PlayerHealth : MonoBehaviour
{
    [Header("Health System")]
    public float maxHealth = 200f;
    private float currentHealth;
    public Slider healthBar; // Drag your PlayerHealthBar UI here!

    [Header("References")]
    public Animator anim; // Optional: for FPS hand death/respawn animations

    private CharacterController controller;
    private Vector3 startPosition;

    void Start()
    {
        // We need the CharacterController to disable it during teleportation
        controller = GetComponent<CharacterController>();

        // Initialize Health
        currentHealth = maxHealth;
        if (healthBar != null) healthBar.value = currentHealth;

        // Save the exact spot the player spawns in at
        startPosition = transform.position;
    }

    public void TakeDamage(float amount)
    {
        // Prevent taking damage if already dead
        if (currentHealth <= 0) return;

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

        // Reset the kill streak audio
        if (KillManager.Instance != null)
        {
            KillManager.Instance.ResetStreakOnDeath();
        }

        // Optional: anim.SetTrigger("Die");

        StartCoroutine(RespawnRoutine());
    }

    IEnumerator RespawnRoutine()
    {
        // Wait 1 second before respawning
        yield return new WaitForSeconds(1f);

        // Turn off controller to allow teleportation (prevents Unity physics from overriding the move)
        if (controller != null) controller.enabled = false;

        // Teleport back to start
        transform.position = startPosition;

        // Refill health
        currentHealth = maxHealth;
        if (healthBar != null) healthBar.value = currentHealth;

        // Optional: if (anim != null) anim.Play("Idle"); 

        // Turn controller back on so player can move again
        if (controller != null) controller.enabled = true;
    }
}