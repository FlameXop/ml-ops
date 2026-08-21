using UnityEngine;
using System.Collections;

public class Gun : MonoBehaviour
{
    [Header("Combat Stats")]
    public float damage = 25f; // Updated to match your old script's 25f
    public float range = 100f;
    public float shootDelay = 0.2f; // Updated to match your old fireRate
    private float lastShootTime;

    [Header("References")]
    public Camera fpsCam;
    public Transform bulletSpawnPoint;
    public ParticleSystem muzzleFlash;
    public GameObject bulletImpact;
    public TrailRenderer bulletTrail;
    public GunAnimation gunAnimation;

    [Header("Audio (Migrated)")]
    public AudioClip shootSound;
    public AudioSource audioSource;

    [Header("Ammo & Reloading")]
    public int maxAmmo = 10;
    public int currentAmmo;
    public float reloadTime = 2f;
    private bool isReloading = false;

    [Header("Recoil & Spread")]
    public bool addBulletSpread = true;
    public Vector3 bulletSpreadVariance = new Vector3(0.1f, 0.1f, 0.1f);

    private void Start()
    {
        currentAmmo = maxAmmo;
    }

    void Update()
    {
        if (isReloading)
            return;

        if (currentAmmo <= 0)
        {
            StartCoroutine(Reload());
            return;
        }

        if (Input.GetMouseButton(0))
        {
            Shoot();
        }
    }

    IEnumerator Reload()
    {
        isReloading = true;
        if (gunAnimation != null) gunAnimation.PlayReload();

        yield return new WaitForSeconds(reloadTime);

        currentAmmo = maxAmmo;
        isReloading = false;

        if (gunAnimation != null) gunAnimation.PlayIdle();
    }

    void Shoot()
    {
        if (Time.time < lastShootTime + shootDelay)
            return;

        lastShootTime = Time.time;
        currentAmmo--;

        // --- MIGRATED: Audio Setup ---
        if (shootSound != null && audioSource != null)
        {
            audioSource.Stop();
            audioSource.pitch = Random.Range(0.95f, 1.05f);
            audioSource.clip = shootSound;
            audioSource.Play();
        }

        // --- Visuals ---
        if (gunAnimation != null) gunAnimation.PlayFire();
        if (muzzleFlash != null) muzzleFlash.Play();

        Vector3 direction = GetDirection();

        // --- MIGRATED: LayerMask & Trigger Bypass ---
        // Ignore the Player's own body so you don't block your own bullets
        int layerMask = ~LayerMask.GetMask("Player", "Ignore Raycast");

        // Use QueryTriggerInteraction.Ignore so bullets bypass NPC vision cones/detection triggers
        if (Physics.Raycast(fpsCam.transform.position, direction, out RaycastHit hit, range, layerMask, QueryTriggerInteraction.Ignore))
        {
            if (bulletTrail != null)
            {
                TrailRenderer trail = Instantiate(bulletTrail, bulletSpawnPoint.position, Quaternion.identity);
                StartCoroutine(SpawnTrail(trail, hit));
            }

            // --- MIGRATED: Specific Enemy Logic ---
            // Use GetComponentInParent so hitting ANY part of the enemy works
            EnemeyGoalAi enemy = hit.collider.GetComponentInParent<EnemeyGoalAi>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage, hit.point, hit.normal);
            }
        }
    }

    private Vector3 GetDirection()
    {
        Vector3 direction = fpsCam.transform.forward;

        if (addBulletSpread)
        {
            direction += new Vector3(
                Random.Range(-bulletSpreadVariance.x, bulletSpreadVariance.x),
                Random.Range(-bulletSpreadVariance.y, bulletSpreadVariance.y),
                Random.Range(-bulletSpreadVariance.z, bulletSpreadVariance.z)
            );
            direction.Normalize();
        }
        return direction;
    }

    public IEnumerator SpawnTrail(TrailRenderer trail, RaycastHit hit)
    {
        float bulletSpeed = 300f;
        float distance = Vector3.Distance(trail.transform.position, hit.point);
        float duration = distance / bulletSpeed;
        float time = 0f;
        Vector3 start = trail.transform.position;
        Vector3 end = hit.point;

        while (time < duration)
        {
            trail.transform.position = Vector3.Lerp(start, end, time / duration);
            time += Time.deltaTime;
            yield return null;
        }

        trail.transform.position = end;

        if (bulletImpact != null)
        {
            GameObject impact = Instantiate(bulletImpact, hit.point, Quaternion.LookRotation(hit.normal));
            Destroy(impact, 1.5f);
        }

        Destroy(trail.gameObject, trail.time + 0.3f);
    }
}