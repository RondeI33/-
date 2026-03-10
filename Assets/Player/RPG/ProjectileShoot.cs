using UnityEngine;
public class ProjectileShoot : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private GameObject rocketVisual;
    [SerializeField] private float projectileSpeed = 30f;
    [SerializeField] private ParticleSystem fireParticle;
    [SerializeField] private AudioSource shootAudioSource;
    [SerializeField] private AudioClip shootSound;
    [SerializeField] private float pitchMin = 0.9f;
    [SerializeField] private float pitchMax = 1.1f;
    private WeaponRecoil weaponRecoil;
    private void Start()
    {
        weaponRecoil = GetComponent<WeaponRecoil>();
        weaponRecoil.OnFired += Shoot;
    }
    private void OnDestroy()
    {
        if (weaponRecoil)
            weaponRecoil.OnFired -= Shoot;
    }
    private void Shoot()
    {
        fireParticle.Play();
        if (shootAudioSource && shootSound)
        {
            shootAudioSource.pitch = Random.Range(pitchMin, pitchMax);
            shootAudioSource.clip = shootSound;
            shootAudioSource.Play();
        }
        Vector3 direction = playerCamera.transform.forward;
        Vector3 position = spawnPoint ? spawnPoint.position : playerCamera.transform.position + direction * 0.5f;
        Quaternion rotation = Quaternion.LookRotation(direction);
        GameObject rocket = Instantiate(projectilePrefab, position, rotation);
        Rigidbody rb = rocket.GetComponent<Rigidbody>();
        if (rb != null)
            rb.linearVelocity = direction * projectileSpeed;
        if (rocketVisual != null)
            rocketVisual.SetActive(false);
    }
    public void ShowRocketVisual()
    {
        if (rocketVisual != null)
            rocketVisual.SetActive(true);
    }
}