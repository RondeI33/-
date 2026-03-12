using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

public class WeaponController : MonoBehaviour
{
    [SerializeField] Transform firePoint;
    [SerializeField] Camera playerCamera;
    [SerializeField] LayerMask hitLayers;
    [SerializeField] float fireRate = 0.15f;
    [SerializeField] bool autoFire = true;
    [SerializeField] int projectilePoolSize = 30;

    [Header("Decals")]
    [SerializeField] GameObject decalPrefab;
    [SerializeField] int maxDecals = 50;
    [SerializeField] float decalOffset = 0.016f;
    [SerializeField] float fadeDuration = 2f;
    private static readonly Color[] hitColors = new Color[]
    {
        new Color(0.8f, 0.3f, 0.9f, 1f),
        new Color(1f, 1f, 0.3f, 1f)
    };

    private PlayerInput playerInput;
    private InputAction attackAction;
    private IFireSource[] cachedSources;
    private IShotModifier[] cachedModifiers;
    private GameObject[] decalPool;
    private Material[] decalMats;
    private DecalFade[] decalFaders;
    private int decalIndex;
    private int renderQueue = 3001;
    private float nextFireTime;
    private Dictionary<GameObject, Queue<GameObject>> projectilePools = new Dictionary<GameObject, Queue<GameObject>>();

    void Start()
    {
        playerInput = GetComponentInParent<PlayerInput>();
        attackAction = playerInput.actions["Attack"];
        RefreshModuleCache();

        decalPool = new GameObject[maxDecals];
        decalMats = new Material[maxDecals];
        decalFaders = new DecalFade[maxDecals];
    }

    public void RefreshModuleCache()
    {
        cachedSources = GetComponents<IFireSource>();
        cachedModifiers = GetComponents<IShotModifier>();
    }

    void Update()
    {
        bool wantsFire = autoFire
            ? attackAction.IsPressed()
            : attackAction.WasPressedThisFrame();

        if (wantsFire && Time.time >= nextFireTime)
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

    public void Fire()
    {
        if (cachedSources.Length == 0) return;

        List<ShotData> allShots = new List<ShotData>();

        for (int i = 0; i < cachedSources.Length; i++)
        {
            List<ShotData> shots = cachedSources[i].CreateShots(i, cachedSources.Length);
            foreach (ShotData shot in shots)
            {
                if (shot.origin == Vector3.zero)
                    shot.origin = firePoint.position;
                if (shot.direction == Vector3.zero)
                    shot.direction = firePoint.forward;
                shot.hitLayers = hitLayers;
                shot.weaponController = this;
            }
            allShots.AddRange(shots);
        }

        allShots = RunPipeline(allShots);
        ExecuteShots(allShots);
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
            shots = modifier.ProcessShots(shots);
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

                target.TakeDamage(shot.damage, info);
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

    void SpawnDecal(RaycastHit hit)
    {
        if (decalPrefab == null) return;
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
}