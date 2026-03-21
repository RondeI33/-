using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

public class WeaponController : MonoBehaviour
{
    [SerializeField] Transform firePoint;
    [SerializeField] ParticleSystem fireParticle;
    [SerializeField] Camera playerCamera;
    [SerializeField] LayerMask hitLayers;
    [SerializeField] float fireRate = 0.15f;
    [SerializeField] bool autoFire = true;


    [Header("Decals")]
    [SerializeField] GameObject decalPrefab;
    [SerializeField] int maxDecals = 50;
    [SerializeField] float decalOffset = 0.016f;
    [SerializeField] float fadeDuration = 2f;
    [SerializeField] float decalLifetime = 5f;
    private static readonly Color[] hitColors = new Color[]
    {
        new Color(0.8f, 0.3f, 0.9f, 1f),
        new Color(1f, 1f, 0.3f, 1f)
    };

    [Header("Weakpoint")]
    [SerializeField] float weakpointMultiplier = 2f;

    [Header("Sound")]
    [SerializeField] private AudioSource shootAudioSource;
    [SerializeField] private AudioClip shootSound;
    [SerializeField] private float pitchMin = 0.9f;
    [SerializeField] private float pitchMax = 1.1f;

    [Header("Hit Feedback")]
    [SerializeField] private AudioSource hitAudioSource;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private float hitPitchMin = 0.9f;
    [SerializeField] private float hitPitchMax = 1.1f;

    private PlayerInput playerInput;
    private InputAction attackAction;
    private IFireSource[] cachedSources;
    private IShotModifier[] cachedModifiers;
    private HitPopUp hitPopUp;
    private ImmunePopUp immunePopUp;
    private GameObject[] decalPool;
    private Material[] decalMats;
    private DecalFade[] decalFaders;
    private float[] decalSpawnTimes;
    private int decalIndex;
    private int decalLayerCounter;
    private const int decalBaseQueue = 2001;
    private const int decalMaxQueue = 3000;
    private float nextFireTime;
    private bool reloadBlocked;
    private Dictionary<GameObject, Queue<GameObject>> projectilePools = new Dictionary<GameObject, Queue<GameObject>>();
    public System.Action OnFired;

    void Start()
    {
        playerInput = GetComponentInParent<PlayerInput>();
        attackAction = playerInput.actions["Attack"];
        hitPopUp = FindFirstObjectByType<HitPopUp>();
        immunePopUp = FindFirstObjectByType<ImmunePopUp>();
        RefreshModuleCache();

        decalPool = new GameObject[maxDecals];
        decalMats = new Material[maxDecals];
        decalFaders = new DecalFade[maxDecals];
        decalSpawnTimes = new float[maxDecals];
    }

    public void RefreshModuleCache()
    {
        cachedSources = GetComponents<IFireSource>();
        cachedModifiers = GetComponents<IShotModifier>();
    }

    void Update()
    {
        bool anyActive = false;
        for (int i = 0; i < maxDecals; i++)
        {
            if (decalPool[i] != null && decalPool[i].activeSelf)
            {
                if (Time.time - decalSpawnTimes[i] >= decalLifetime)
                    decalPool[i].SetActive(false);
                else
                    anyActive = true;
            }
        }

        if (!anyActive)
            decalLayerCounter = 0;

        bool wantsFire = autoFire
            ? attackAction.IsPressed()
            : attackAction.WasPressedThisFrame();

        if (!reloadBlocked && wantsFire && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireRate;
            Fire();
        }
    }

    void OnDestroy()
    {
        if (decalMats == null) return;
        for (int i = 0; i < decalMats.Length; i++)
        {
            if (decalMats[i])
                Destroy(decalMats[i]);
        }
    }

    Vector3 GetSafeFireOrigin()
    {
        Vector3 camPos = playerCamera.transform.position;
        Vector3 toFirePoint = firePoint.position - camPos;
        float dist = toFirePoint.magnitude;

        if (Physics.Raycast(camPos, toFirePoint.normalized, out RaycastHit hit, dist, hitLayers))
        {
            Collider[] nearby = Physics.OverlapSphere(firePoint.position, 0.3f, 1 << 2);
            for (int i = 0; i < nearby.Length; i++)
            {
                if (nearby[i].GetComponent<Portal>() != null)
                    return firePoint.position;
            }

            return hit.point + hit.normal * 0.05f;
        }

        return firePoint.position;
    }

    public void Fire()
    {
        RefreshModuleCache();

        fireParticle.Play();
        PlaySound(shootAudioSource, shootSound);

        int activeSources = 0;
        for (int i = 0; i < cachedSources.Length; i++)
        {
            if (((MonoBehaviour)cachedSources[i]).isActiveAndEnabled)
                activeSources++;
        }
        if (activeSources == 0) return;

        Vector3 safeOrigin = GetSafeFireOrigin();
        bool originWasOverridden = safeOrigin != firePoint.position;


        Vector3 camOrigin = playerCamera.transform.position;
        Vector3 camForward = playerCamera.transform.forward;
        Vector3 aimPoint = Physics.Raycast(camOrigin, camForward, out RaycastHit aimHit, 1000f, hitLayers)
            ? aimHit.point
            : camOrigin + camForward * 1000f;

        List<ShotData> allShots = new List<ShotData>();
        int activeIndex = 0;

        for (int i = 0; i < cachedSources.Length; i++)
        {
            if (!((MonoBehaviour)cachedSources[i]).isActiveAndEnabled) continue;

            List<ShotData> shots = cachedSources[i].CreateShots(activeIndex, activeSources);
            foreach (ShotData shot in shots)
            {
                shot.origin = safeOrigin;

                if (originWasOverridden)
                    shot.direction = (aimPoint - safeOrigin).normalized;
                else if (shot.direction == Vector3.zero)
                    shot.direction = firePoint.forward;

                shot.hitLayers = hitLayers;
                shot.weaponController = this;
            }
            allShots.AddRange(shots);
            activeIndex++;
        }

        allShots = RunPipeline(allShots);
        ExecuteShots(allShots);
        OnFired?.Invoke();
    }

    public void FireSecondary(List<ShotData> shots)
    {
        foreach (ShotData shot in shots)
        {
            shot.hitLayers = hitLayers;
            shot.weaponController = this;
        }

        shots = RunPipeline(shots);
        ExecuteShots(shots);
    }

    public float ApplyDamageModifier(float damage, Collider col)
    {
        if (col.CompareTag("IgnoreDamage")) return 0f;
        if (col.CompareTag("Weakpoint")) return damage * weakpointMultiplier;
        return damage;
    }

    public void SetReloadBlocked(bool blocked)
    {
        reloadBlocked = blocked;
    }

    public Transform GetFirePoint()
    {
        return firePoint;
    }

    public Vector3 GetAimDirection()
    {
        Vector3 camOrigin = playerCamera.transform.position;
        Vector3 camForward = playerCamera.transform.forward;

        Vector3 aimPoint;
        if (Physics.Raycast(camOrigin, camForward, out RaycastHit hit, 1000f, hitLayers))
            aimPoint = hit.point;
        else
            aimPoint = camOrigin + camForward * 1000f;

        return (aimPoint - firePoint.position).normalized;
    }

    List<ShotData> RunPipeline(List<ShotData> shots)
    {
        foreach (IShotModifier modifier in cachedModifiers)
        {
            if (!((MonoBehaviour)modifier).isActiveAndEnabled) continue;
            shots = modifier.ProcessShots(shots);
        }
        return shots;
    }

    void ExecuteShots(List<ShotData> shots)
    {
        foreach (ShotData shot in shots)
        {
            if (shot.isRaycast)
                ExecuteRaycast(shot);
            else
                ExecuteProjectile(shot);
        }
    }

    void ExecuteRaycast(ShotData shot)
    {
        HitInfo? hitResult = null;
        Vector3 origin = shot.origin;
        Vector3 direction = shot.direction;
        float remainingDist = shot.maxDistance;
        int bouncesLeft = shot.GetProperty("bouncesLeft", 0);
        bool applyOnBounce = shot.GetProperty("applyEffectsOnBounce", false);

        while (remainingDist > 0f)
        {
            if (!Physics.Raycast(origin, direction, out RaycastHit hit, remainingDist, shot.hitLayers))
                break;

            HitInfo info = new HitInfo(hit);
            hitResult = info;

            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();

            if (target != null)
            {
                foreach (var callback in shot.onHitCallbacks)
                    callback.Invoke(info, shot);

                float dmg = ApplyDamageModifier(shot.damage, hit.collider);
                target.TakeDamage(dmg, info);
                ShowHitFeedback(hit.collider);
                break;
            }

            if (bouncesLeft > 0)
            {
                if (applyOnBounce)
                {
                    foreach (var callback in shot.onHitCallbacks)
                        callback.Invoke(info, shot);
                }

                SpawnDecal(hit);
                direction = Vector3.Reflect(direction, hit.normal);
                remainingDist -= hit.distance;
                origin = hit.point + hit.normal * 0.01f;
                bouncesLeft--;
                continue;
            }

            foreach (var callback in shot.onHitCallbacks)
                callback.Invoke(info, shot);

            SpawnDecal(hit);
            break;
        }

        foreach (var callback in shot.onPostExecute)
            callback.Invoke(hitResult, shot);
    }

    void ExecuteProjectile(ShotData shot)
    {
        if (shot.projectilePrefab == null) return;

        GameObject go = GetPooledProjectile(shot.projectilePrefab);
        go.transform.position = shot.origin;
        go.transform.rotation = Quaternion.LookRotation(shot.direction);
        go.SetActive(true);

        ShotProjectile proj = go.GetComponent<ShotProjectile>();
        if (proj != null)
            proj.Init(shot);
    }

    GameObject GetPooledProjectile(GameObject prefab)
    {
        if (!projectilePools.ContainsKey(prefab))
            projectilePools[prefab] = new Queue<GameObject>();

        Queue<GameObject> pool = projectilePools[prefab];

        while (pool.Count > 0)
        {
            GameObject pooled = pool.Dequeue();
            if (pooled != null && !pooled.activeInHierarchy)
                return pooled;
        }

        GameObject go = Instantiate(prefab);
        go.SetActive(false);
        return go;
    }

    public void ReturnToPool(GameObject go, GameObject prefab)
    {
        go.SetActive(false);

        if (!projectilePools.ContainsKey(prefab))
            projectilePools[prefab] = new Queue<GameObject>();

        projectilePools[prefab].Enqueue(go);
    }

    public void ShowHitFeedback(Collider hitCollider)
    {
        bool isWeakpoint = hitCollider.CompareTag("Weakpoint");
        bool isBlocking = hitCollider.CompareTag("IgnoreDamage");

        if (isBlocking)
        {
            if (immunePopUp)
                immunePopUp.ShowHit(isWeakpoint);
        }
        else
        {
            if (hitPopUp)
                hitPopUp.ShowHit(isWeakpoint);
            if (hitAudioSource && hitSound)
            {
                hitAudioSource.pitch = Random.Range(hitPitchMin, hitPitchMax);
                hitAudioSource.clip = hitSound;
                hitAudioSource.Play();
            }
        }
    }
    public void ShowHitFeedback(Collider hitCollider, bool playSound = true)
    {
        bool isWeakpoint = hitCollider.CompareTag("Weakpoint");
        bool isBlocking = hitCollider.CompareTag("IgnoreDamage");

        if (isBlocking)
        {
            if (immunePopUp)
                immunePopUp.ShowHit(isWeakpoint);
        }
        else
        {
            if (hitPopUp)
                hitPopUp.ShowHit(isWeakpoint);
            if (playSound && hitAudioSource && hitSound)
            {
                hitAudioSource.pitch = Random.Range(hitPitchMin, hitPitchMax);
                hitAudioSource.clip = hitSound;
                hitAudioSource.Play();
            }
        }
    }

    void SpawnDecal(RaycastHit hit)
    {
        SpawnDecal(hit.point, hit.normal, hit.collider);
    }

    public void SpawnDecal(Vector3 point, Vector3 normal, Collider col)
    {
        if (decalPrefab == null) return;
        if (col.gameObject.layer == LayerMask.NameToLayer("Door")) return;
        if (normal.sqrMagnitude < 0.0001f) return;

        Vector3 position = point + normal * decalOffset;
        Quaternion rotation = Quaternion.LookRotation(-normal);

        if (decalPool[decalIndex] == null)
        {
            decalPool[decalIndex] = Instantiate(decalPrefab, position, rotation);
            Renderer rend = decalPool[decalIndex].GetComponent<Renderer>();
            decalMats[decalIndex] = new Material(rend.material);
            decalMats[decalIndex].SetFloat("_ZWrite", 0f);
            rend.material = decalMats[decalIndex];
            decalFaders[decalIndex] = decalPool[decalIndex].AddComponent<DecalFade>();
        }
        else
        {
            decalPool[decalIndex].transform.position = position;
            decalPool[decalIndex].transform.rotation = rotation;
            decalPool[decalIndex].SetActive(true);
        }

        if (decalBaseQueue + decalLayerCounter > decalMaxQueue)
        {
            for (int i = 0; i < maxDecals; i++)
            {
                if (decalPool[i] != null && decalPool[i].activeSelf && i != decalIndex)
                    decalPool[i].SetActive(false);
            }
            decalLayerCounter = 0;
        }

        Color color = hitColors[Random.Range(0, hitColors.Length)];
        decalMats[decalIndex].SetColor("_BaseColor", color);
        decalMats[decalIndex].renderQueue = decalBaseQueue + decalLayerCounter;
        decalLayerCounter++;

        Transform target = col.transform;
        Vector3 localPos = target.InverseTransformPoint(position);
        Quaternion localRot = Quaternion.Inverse(target.rotation) * rotation;
        decalFaders[decalIndex].Init(decalMats[decalIndex], color, fadeDuration, target, localPos, localRot);
        decalSpawnTimes[decalIndex] = Time.time;
        decalIndex = (decalIndex + 1) % maxDecals;
    }

    private void PlaySound(AudioSource source, AudioClip clip, float pitch = -1f)
    {
        if (source == null || clip == null) return;
        source.pitch = pitch < 0f ? Random.Range(pitchMin, pitchMax) : pitch;
        source.clip = clip;
        source.PlayOneShot(clip);
        //source.Play();
    }
}