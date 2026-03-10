using UnityEngine;
using System.Collections;

public class ShotgunShoot : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private GameObject decalPrefab;

    [Header("Shotgun Stats")]
    [SerializeField] private float maxDistance = 40f;
    [SerializeField] private int pelletsPerShot = 8;
    [SerializeField] private float spreadAngle = 6f;
    [SerializeField] private float damage = 14f;
    [SerializeField] private float weakpointMultiplier = 1.6f;

    [Header("Double Barrel")]
    [SerializeField] private int maxAmmo = 2;
    [SerializeField] private float reloadTime = 2.2f;

    [Header("Decals")]
    [SerializeField] private LayerMask excludeLayers;
    [SerializeField] private int maxDecals = 50;
    [SerializeField] private float decalOffset = 0.016f;
    [SerializeField] private float fadeDuration = 1.5f;

    [Header("Particle")]
    [SerializeField] private ParticleSystem fireParticle;

    [Header("Sound Effects")]
    [SerializeField] private AudioSource shootAudioSource;
    [SerializeField] private AudioClip shootSound;
    [SerializeField] private AudioSource hitAudioSource;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private float pitchMin = 0.9f;
    [SerializeField] private float pitchMax = 1.1f;

    private WeaponRecoil weaponRecoil;
    private HitPopUp hitPopUp;
    private ImmunePopUp immunePopUp;

    private GameObject[] decalPool;
    private Material[] decalMats;
    private DecalFade[] decalFaders;
    private int decalIndex;
    private int renderQueue = 3001;

    private int currentAmmo;
    private bool isReloading = false;

    private static readonly Color[] hitColors = new Color[]
    {
        new Color(0.8f, 0.3f, 0.9f, 1f),
        new Color(1f, 1f, 0.3f, 1f)
    };

    private void Start()
    {
        weaponRecoil = GetComponent<WeaponRecoil>();
        weaponRecoil.OnFired += TryShoot;

        hitPopUp = FindFirstObjectByType<HitPopUp>();
        immunePopUp = FindFirstObjectByType<ImmunePopUp>();

        currentAmmo = maxAmmo;

        decalPool = new GameObject[maxDecals];
        decalMats = new Material[maxDecals];
        decalFaders = new DecalFade[maxDecals];
    }

    private void OnDestroy()
    {
        if (weaponRecoil)
            weaponRecoil.OnFired -= TryShoot;

        if (decalMats == null) return;
        for (int i = 0; i < decalMats.Length; i++)
        {
            if (decalMats[i])
                Destroy(decalMats[i]);
        }
    }

    private void TryShoot()
    {
        if (isReloading || currentAmmo <= 0) return;

        currentAmmo--;
        Shoot();

        if (currentAmmo <= 0)
            StartCoroutine(Reload());
    }
    private void OnEnable()
    {
        isReloading = false;
        currentAmmo = maxAmmo;
    }
    private void Shoot()
    {
        PlaySound(shootAudioSource, shootSound);

        int layerMask = ~(1 << LayerMask.NameToLayer("Player"));
        bool anyHitEnemy = false;
        bool anyWeakpoint = false;
        bool anyBlocked = false;

        for (int i = 0; i < pelletsPerShot; i++)
        {
            Vector3 spread = Random.insideUnitCircle * Mathf.Tan(spreadAngle * Mathf.Deg2Rad);
            Vector3 direction = playerCamera.transform.forward
                              + playerCamera.transform.right * spread.x
                              + playerCamera.transform.up * spread.y;
            direction.Normalize();

            Ray ray = new Ray(playerCamera.transform.position, direction);

            fireParticle.Play();

            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, layerMask, QueryTriggerInteraction.Ignore))
            {
                IEnemy enemy = hit.collider.GetComponentInParent<IEnemy>();
                if (enemy != null)
                {
                    bool isWeakpoint = hit.collider.CompareTag("Weakpoint");
                    float finalDamage = isWeakpoint ? damage * weakpointMultiplier : damage;

                    bool isBlocking = hit.collider.CompareTag("IgnoreDamage");
                    float finalFinalDamage = isBlocking ? 0 : finalDamage;

                    enemy.TakeDamage(finalFinalDamage, hit.point, hit.normal);

                    if (!isBlocking)
                    {
                        anyHitEnemy = true;
                        if (isWeakpoint) anyWeakpoint = true;
                    }
                    else
                    {
                        anyBlocked = true;
                    }
                }
                else
                {
                    SpawnDecal(hit);
                }
            }
        }

        if (anyHitEnemy && hitPopUp)
        {
            hitPopUp.ShowHit(anyWeakpoint);
            float hitPitch = Random.Range(pitchMin, pitchMax) + (anyWeakpoint ? 0.5f : 0f);
            PlaySound(hitAudioSource, hitSound, hitPitch);
        }
        else if (anyBlocked && immunePopUp)
        {
            immunePopUp.ShowHit(false);
            PlaySound(hitAudioSource, hitSound, Random.Range(pitchMin, pitchMax) - 0.5f);
        }
    }

    private void PlaySound(AudioSource source, AudioClip clip, float pitch = -1f)
    {
        if (source == null || clip == null) return;
        source.pitch = pitch < 0f ? Random.Range(pitchMin, pitchMax) : pitch;
        source.clip = clip;
        source.Play();
    }

    private IEnumerator Reload()
    {
        isReloading = true;
        yield return new WaitForSeconds(reloadTime);
        currentAmmo = maxAmmo;
        isReloading = false;
    }

    private void SpawnDecal(RaycastHit hit)
    {
        if (hit.collider.gameObject.layer == LayerMask.NameToLayer("Door")) return;

        Vector3 position = hit.point + hit.normal * decalOffset;
        Quaternion rotation = Quaternion.LookRotation(-hit.normal);

        if (decalPool[decalIndex] == null)
        {
            decalPool[decalIndex] = Instantiate(decalPrefab, position, rotation);
            Renderer rend = decalPool[decalIndex].GetComponent<Renderer>();
            decalMats[decalIndex] = new Material(rend.material);
            rend.material = decalMats[decalIndex];
            decalFaders[decalIndex] = decalPool[decalIndex].AddComponent<DecalFade>();
        }
        else
        {
            decalPool[decalIndex].transform.position = position;
            decalPool[decalIndex].transform.rotation = rotation;
        }

        Color color = hitColors[Random.Range(0, hitColors.Length)];
        decalMats[decalIndex].SetColor("_BaseColor", color);
        decalMats[decalIndex].renderQueue = renderQueue;
        renderQueue++;
        if (renderQueue >= 4000)
            renderQueue = 3001;

        Transform target = hit.collider.transform;
        Vector3 localPos = target.InverseTransformPoint(position);
        Quaternion localRot = Quaternion.Inverse(target.rotation) * rotation;
        decalFaders[decalIndex].Init(decalMats[decalIndex], color, fadeDuration, target, localPos, localRot);

        decalIndex = (decalIndex + 1) % maxDecals;
    }


    public int CurrentAmmo => currentAmmo;
    public bool IsReloading => isReloading;
}