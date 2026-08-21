using UnityEngine;
using System.Collections;

public class GunAnimation : MonoBehaviour
{
    public Animator animator;
    public Transform recoilTransform;

    public float recoilAmount = 12f;
    public float recoilSpeed = 18f;
    public float recoilReturnSpeed = 14f;

    private Vector3 recoilCurrent;
    private Vector3 recoilTarget;

    private bool isReloading = false;
    private bool isFiring = false;

    void Update()
    {
        // Recoil smoothing
        recoilCurrent = Vector3.Lerp(recoilCurrent, recoilTarget, Time.deltaTime * recoilSpeed);
        recoilTransform.localEulerAngles = recoilCurrent;

        // Return to neutral
        recoilTarget = Vector3.Lerp(recoilTarget, Vector3.zero, Time.deltaTime * recoilReturnSpeed);
    }

    // ---------------- ANIMATION CONTROL ----------------

    public void PlayFire()
    {
        if (isReloading) return;

        StartCoroutine(FireRoutine());
    }

    IEnumerator FireRoutine()
    {
        isFiring = true;

        animator.Play("Fire", 0, 0f);

        // Wait one frame so we get the correct animation length
        yield return null;

        float len = animator.GetCurrentAnimatorStateInfo(0).length;

        // Add recoil
        recoilTarget += new Vector3(-recoilAmount, 0, 0);

        yield return new WaitForSeconds(len);

        isFiring = false;

        if (!isReloading)
            animator.Play("Idle");
    }


    public void PlayReload()
    {
        if (isReloading) return;

        StartCoroutine(ReloadRoutine());
    }

    IEnumerator ReloadRoutine()
    {
        isReloading = true;

        // Play the reload animation from start
        animator.Play("Reload", 0, 0f);

        // Wait one frame so Unity updates the animator
        yield return null;

        // Now the animator is actually in the Reload state
        float len = animator.GetCurrentAnimatorStateInfo(0).length;

        // Wait exactly the length of the reload animation
        yield return new WaitForSeconds(len);

        isReloading = false;

        if (!isFiring)
            animator.Play("Idle");
    }


    public void PlayIdle()
    {
        if (!isFiring && !isReloading)
            animator.Play("Idle");
    }
}
