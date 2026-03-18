using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ShotProjectile : PortalTraveller
{
    [SerializeField] float lifetime = 5f;
    [SerializeField] float maxRange = 200f;
    [SerializeField] float speedTrailThreshold = 167f;
    [SerializeField] Color speedTrailColor = new Color(1f, 0.85f, 0.3f, 1f);
    [SerializeField] float speedTrailFadeTime = 0.4f;

    private ShotData shotData;
    private Rigidbody rb;
    private bool hasHit;
    private float splitTotalTime;
    private float splitFireTime;
    private bool hasSplit;
    private float despawnTime;
    private GameObject prefabSource;
    private Vector3 spawnPosition;

    private TrailRenderer[] trails;
    private bool useSpeedTrail;
    private Vector3 trailSegmentStart;
    private AnimationCurve trailWidthCurve;
    private float trailWidthMultiplier;

    private Vector3 cameraDirection;
    private Vector3 cameraPosition;
    private bool firstBounce;
    private Vector3 previousPosition;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        trails = GetComponentsInChildren<TrailRenderer>();
        travellerType = PortalTravellerType.Projectile;
    }

    public override void Teleport(Transform fromPortal, Transform toPortal, Vector3 pos, Quaternion rot)
    {
        foreach (var trail in trails)
        {
            trail.emitting = false;
            trail.Clear();
        }

        Matrix4x4 portalMatrix = toPortal.localToWorldMatrix
            * Matrix4x4.Rotate(Quaternion.Euler(0f, 180f, 0f))
            * fromPortal.worldToLocalMatrix;

        float speed = rb.linearVelocity.magnitude;
        Vector3 newCamDir = portalMatrix.MultiplyVector(cameraDirection).normalized;
        Vector3 virtualCamPos = portalMatrix.MultiplyPoint3x4(cameraPosition);

        Vector3 farTarget = virtualCamPos + newCamDir * maxRange;
        Vector3 newVel = (farTarget - pos).normalized * speed;
        Quaternion newRot = Quaternion.LookRotation(newVel.normalized);

        transform.position = pos;
        transform.rotation = newRot;
        rb.position = pos;
        rb.rotation = newRot;
        rb.linearVelocity = newVel;
        rb.angularVelocity = Vector3.zero;
        cameraDirection = newCamDir;
        cameraPosition = virtualCamPos;
        spawnPosition = pos;
        trailSegmentStart = pos;
        previousPosition = pos;

        Physics.SyncTransforms();
        lastTeleportTime = Time.time;

        foreach (var trail in trails)
        {
            trail.Clear();
            trail.emitting = true;
        }
    }

    public override void EnterPortalThreshold()
    {
        if (graphicsObject == null)
        {
            if (graphicsClone == null)
            {
                graphicsClone = new GameObject("ProjectileClone");
                graphicsClone.transform.parent = transform.parent;
                originalMaterials = new Material[0];
                cloneMaterials = new Material[0];
            }
            else
            {
                graphicsClone.SetActive(true);
            }
            return;
        }
        base.EnterPortalThreshold();
    }

    public override void ExitPortalThreshold()
    {
        if (graphicsClone != null)
            graphicsClone.SetActive(false);
        if (originalMaterials != null)
        {
            for (int i = 0; i < originalMaterials.Length; i++)
                originalMaterials[i].SetVector("sliceNormal", Vector3.zero);
        }
    }

    void OnDisable()
    {
        if (graphicsClone != null)
            graphicsClone.SetActive(false);
    }

    public void Init(ShotData data)
    {
        shotData = data;
        hasHit = false;
        hasSplit = false;

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        prefabSource = data.projectilePrefab;
        splitTotalTime = data.GetProperty("splitTotalTime", 0f);
        splitFireTime = data.GetProperty("splitFireTime", 0f);
        despawnTime = Time.time + lifetime;
        spawnPosition = transform.position;
        previousPosition = transform.position;

        cameraDirection = Camera.main.transform.forward;
        cameraPosition = Camera.main.transform.position;
        firstBounce = true;

        useSpeedTrail = data.speed > speedTrailThreshold;
        trailSegmentStart = spawnPosition;

        if (useSpeedTrail && trails.Length > 0)
        {
            TrailRenderer src = trails[0];
            AnimationCurve original = src.widthCurve;
            trailWidthMultiplier = src.widthMultiplier;

            Keyframe[] keys = original.keys;
            Keyframe[] reversed = new Keyframe[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                Keyframe k = keys[keys.Length - 1 - i];
                reversed[i] = new Keyframe(1f - k.time, k.value, -k.outTangent, -k.inTangent);
            }
            trailWidthCurve = new AnimationCurve(reversed);
        }

        bool useGravity = data.GetProperty("useGravity", false);
        float lobAngle = data.GetProperty("lobAngle", 0f);

        rb.useGravity = useGravity;

        Vector3 velocity = data.direction.normalized * data.speed;

        if (useGravity && lobAngle > 0f)
            velocity = Quaternion.AngleAxis(-lobAngle, transform.right) * velocity;

        rb.linearVelocity = velocity;

        if (trails.Length > 0)
            StartCoroutine(ClearTrailsNextFrame());
    }

    private IEnumerator ClearTrailsNextFrame()
    {
        foreach (var trail in trails)
        {
            trail.emitting = false;
            trail.Clear();
        }

        yield return null;

        foreach (var trail in trails)
        {
            trail.Clear();
            trail.emitting = true;
        }
    }

    private void SpawnTrailSegment(Vector3 from, Vector3 to)
    {
        if (!useSpeedTrail) return;
        SpeedTrail.Spawn(from, to, trailWidthCurve, trailWidthMultiplier, speedTrailFadeTime, speedTrailColor);
    }

    void Update()
    {
        if (shotData == null) return;

        if (Time.time >= despawnTime || Vector3.Distance(transform.position, spawnPosition) >= maxRange)
        {
            SpawnTrailSegment(trailSegmentStart, transform.position);
            ReturnToPool();
            return;
        }

        if (hasSplit || hasHit) return;
        if (splitTotalTime <= 0f) return;

        float elapsed = Time.time - splitFireTime;
        if (elapsed < splitTotalTime) return;

        hasSplit = true;

        int count = shotData.GetProperty("splitCount", 0);
        float spread = shotData.GetProperty("splitSpread", 15f);
        float dmgMult = shotData.GetProperty("splitDamageMultiplier", 0.6f);
        int moduleId = shotData.GetProperty("splitModuleId", 0);
        float timeUsed = splitTotalTime;

        if (count > 0 && shotData.weaponController != null)
        {
            Vector3 forward = rb.linearVelocity.normalized;
            List<ShotData> fragments = SplitShotModule.CreateFragments(shotData, transform.position, forward, count, spread, dmgMult, shotData.maxDistance, moduleId, timeUsed);
            shotData.weaponController.FireSecondary(fragments);
        }

        SpawnTrailSegment(trailSegmentStart, transform.position);
        ReturnToPool();
    }

    void FixedUpdate()
    {
        if (hasHit || shotData == null) return;

        Vector3 curPos = transform.position;
        if (Portal.TryPassThrough(this, previousPosition, curPos))
        {
            previousPosition = transform.position;
            return;
        }
        previousPosition = curPos;

        Vector3 vel = rb.linearVelocity;
        float speed = vel.magnitude;
        if (speed < 0.01f) return;

        Vector3 dir = vel / speed;
        float frameDist = speed * Time.fixedDeltaTime + 0.1f;
        float remainingDist = frameDist;
        Vector3 origin = transform.position;
        int bouncesLeft = shotData.GetProperty("bouncesLeft", 0);
        bool bounced = false;

        while (remainingDist > 0f)
        {
            if (!Physics.Raycast(origin, dir, out RaycastHit hit, remainingDist, shotData.hitLayers))
                break;

            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();

            if (target != null)
            {
                hasHit = true;
                SpawnTrailSegment(trailSegmentStart, hit.point);
                HitInfo info = new HitInfo(hit.point, hit.normal, hit.collider);
                foreach (var callback in shotData.onHitCallbacks)
                    callback.Invoke(info, shotData);
                float dmg = shotData.weaponController != null
                    ? shotData.weaponController.ApplyDamageModifier(shotData.damage, hit.collider)
                    : shotData.damage;
                target.TakeDamage(dmg, info);
                shotData.weaponController?.ShowHitFeedback(hit.collider);
                ReturnToPool();
                return;
            }

            if (bouncesLeft > 0)
            {
                SpawnTrailSegment(trailSegmentStart, hit.point);
                trailSegmentStart = hit.point;

                shotData.weaponController?.SpawnDecal(hit.point, hit.normal, hit.collider);

                bool applyOnBounce = shotData.GetProperty("applyEffectsOnBounce", false);
                if (applyOnBounce)
                {
                    HitInfo info = new HitInfo(hit.point, hit.normal, hit.collider);
                    foreach (var callback in shotData.onHitCallbacks)
                        callback.Invoke(info, shotData);
                }

                Vector3 reflectInput = firstBounce ? cameraDirection : dir;
                firstBounce = false;
                dir = Vector3.Reflect(reflectInput, hit.normal);
                origin = hit.point + hit.normal * 0.05f;
                bouncesLeft--;
                bounced = true;
                remainingDist = frameDist;
                continue;
            }

            hasHit = true;
            SpawnTrailSegment(trailSegmentStart, hit.point);
            HitInfo finalInfo = new HitInfo(hit.point, hit.normal, hit.collider);
            shotData.weaponController?.SpawnDecal(hit.point, hit.normal, hit.collider);
            foreach (var callback in shotData.onHitCallbacks)
                callback.Invoke(finalInfo, shotData);
            ReturnToPool();
            return;
        }

        if (bounced)
        {
            shotData.SetProperty("bouncesLeft", bouncesLeft);
            transform.position = origin;
            rb.linearVelocity = dir * speed;
            trailSegmentStart = origin;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasHit || shotData == null) return;
        if (other.GetComponent<Portal>() != null) return;

        int otherLayer = 1 << other.gameObject.layer;
        if ((shotData.hitLayers.value & otherLayer) == 0) return;

        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target == null) return;

        hasHit = true;

        Vector3 contactPoint = other.ClosestPoint(transform.position);
        Vector3 contactNormal = (transform.position - contactPoint);
        contactNormal = contactNormal.sqrMagnitude < 0.0001f ? -rb.linearVelocity.normalized : contactNormal.normalized;

        SpawnTrailSegment(trailSegmentStart, contactPoint);

        HitInfo info = new HitInfo(contactPoint, contactNormal, other);
        foreach (var callback in shotData.onHitCallbacks)
            callback.Invoke(info, shotData);
        float dmg = shotData.weaponController != null
            ? shotData.weaponController.ApplyDamageModifier(shotData.damage, other)
            : shotData.damage;
        target.TakeDamage(dmg, info);
        shotData.weaponController?.ShowHitFeedback(other);
        ReturnToPool();
    }

    void ReturnToPool()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        foreach (var trail in trails)
        {
            trail.emitting = false;
            trail.Clear();
        }

        if (shotData != null && shotData.weaponController != null && prefabSource != null)
            shotData.weaponController.ReturnToPool(gameObject, prefabSource);
        else
            gameObject.SetActive(false);

        shotData = null;
    }
}