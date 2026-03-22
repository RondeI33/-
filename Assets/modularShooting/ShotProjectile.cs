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
    [SerializeField] float trailSpinSpeed = 600f; // degrees per second; set to 0 to disable

    private static readonly int PortalLayerMask = 1 << 2;
    private static readonly Matrix4x4 FlipMatrix = Matrix4x4.Rotate(Quaternion.Euler(0f, 180f, 0f));

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

    private Vector3 crosshairAimPoint;
    private bool hasConverged;
    private Vector3 initialLateralOffset;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        trails = GetComponentsInChildren<TrailRenderer>();
        travellerType = PortalTravellerType.Projectile;
    }

    public override void Teleport(Transform fromPortal, Transform toPortal, Vector3 pos, Quaternion rot)
    {
        // Detach trails BEFORE moving the transform — if they stay attached while
        // the transform teleports, the renderer draws a streak from the last point
        // to the new position. Detaching first lets them fade in place at the entry side.
        foreach (var trail in trails)
        {
            trail.emitting = false;
            trail.transform.SetParent(null, true);
            Destroy(trail.gameObject, trail.time > 0f ? trail.time : 1f);
        }
        trails = new TrailRenderer[0];

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

        hasConverged = true;

        Physics.SyncTransforms();
        lastTeleportTime = Time.time;

        // Detach existing trails so their world-space points linger at the entry
        // portal and fade naturally. Then spawn fresh trail children at the exit
        // so the bullet continues with a clean trail — no flash or pop.
        if (prefabSource != null)
        {
            TrailRenderer[] prefabTrails = prefabSource.GetComponentsInChildren<TrailRenderer>();
            foreach (var trail in trails)
            {
                trail.emitting = false;
                trail.transform.SetParent(null, true);
                Destroy(trail.gameObject, trail.time > 0f ? trail.time : 1f);
            }
            trails = new TrailRenderer[0];
            foreach (TrailRenderer pt in prefabTrails)
            {
                GameObject copy = Instantiate(pt.gameObject, transform);
                copy.transform.localPosition = pt.transform.localPosition;
                copy.transform.localRotation = pt.transform.localRotation;
                TrailRenderer tr = copy.GetComponent<TrailRenderer>();
                tr.Clear();
                tr.emitting = false;
            }
            trails = GetComponentsInChildren<TrailRenderer>();
            // Wait one frame before enabling so TrailRenderer doesn't draw a
            // straight line from its spawn point on the first frame.
            StartCoroutine(EnableTrailsNextFrame());
        }
        else
        {
            foreach (var trail in trails)
            {
                trail.Clear();
                trail.emitting = false;
            }
            StartCoroutine(EnableTrailsNextFrame());
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
            for (int i = 0; i < originalMaterials.Length; i++)
                originalMaterials[i].SetVector("sliceNormal", Vector3.zero);
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
        hasConverged = false;

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        prefabSource = data.projectilePrefab;
        splitTotalTime = data.GetProperty("splitTotalTime", 0f);
        splitFireTime = data.GetProperty("splitFireTime", 0f);
        despawnTime = Time.time + lifetime;
        spawnPosition = transform.position;

        cameraDirection = Camera.main.transform.forward;
        cameraPosition = Camera.main.transform.position;
        firstBounce = true;

        useSpeedTrail = data.speed > speedTrailThreshold;
        trailSegmentStart = spawnPosition;
        trails = GetComponentsInChildren<TrailRenderer>();
        // If trails were detached on the last shot, spawn fresh ones from the prefab.
        if (trails.Length == 0 && data.projectilePrefab != null)
        {
            TrailRenderer[] prefabTrails = data.projectilePrefab.GetComponentsInChildren<TrailRenderer>();
            foreach (TrailRenderer pt in prefabTrails)
            {
                GameObject copy = Instantiate(pt.gameObject, transform);
                copy.transform.localPosition = pt.transform.localPosition;
                copy.transform.localRotation = pt.transform.localRotation;
            }
            trails = GetComponentsInChildren<TrailRenderer>();
        }

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

        if (Physics.Raycast(cameraPosition, cameraDirection, out RaycastHit camHit,
                            maxRange, data.hitLayers, QueryTriggerInteraction.Ignore))
            crosshairAimPoint = camHit.point;
        else
            crosshairAimPoint = cameraPosition + cameraDirection * maxRange;

        const float convergenceDist = 3f;
        Vector3 convPoint = cameraPosition + cameraDirection * convergenceDist;
        Vector3 toConv = convPoint - transform.position;
        Vector3 initialDir = Vector3.Dot(toConv, cameraDirection) > 0.05f
            ? toConv.normalized
            : cameraDirection;

        Vector3 velocity = initialDir * data.speed;
        if (useGravity && lobAngle > 0f)
            velocity = Quaternion.AngleAxis(-lobAngle, transform.right) * velocity;

        rb.linearVelocity = velocity;

        // Record how far the bullet starts from the camera ray so we can
        // lerp it to zero over convergenceDist without any overshoot.
        float spawnProj = Vector3.Dot(transform.position - cameraPosition, cameraDirection);
        Vector3 spawnOnRay = cameraPosition + cameraDirection * spawnProj;
        initialLateralOffset = transform.position - spawnOnRay;

        if (trails.Length > 0)
            StartCoroutine(ClearTrailsNextFrame());
    }

    private IEnumerator EnableTrailsNextFrame()
    {
        yield return null;
        foreach (var trail in trails)
        {
            trail.Clear();
            trail.emitting = true;
        }
    }

    private IEnumerator ClearTrailsNextFrame()
    {
        foreach (var trail in trails)
            trail.emitting = false;
        yield return null;
        // Only disable if convergence hasn't already enabled them this frame.
        if (!hasConverged)
        {
            foreach (var trail in trails)
                trail.emitting = false;
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
            List<ShotData> fragments = SplitShotModule.CreateFragments(
                shotData, transform.position, forward, count, spread,
                dmgMult, shotData.maxDistance, moduleId, timeUsed);
            shotData.weaponController.FireSecondary(fragments);
        }

        SpawnTrailSegment(trailSegmentStart, transform.position);
        ReturnToPool();
    }

    void FixedUpdate()
    {
        if (hasHit || shotData == null) return;

        if (!hasConverged)
        {
            const float convergenceDist = 3f;
            float proj = Vector3.Dot(rb.position - cameraPosition, cameraDirection);
            float spd = rb.linearVelocity.magnitude;
            if (spd > 0.01f)
            {
                // Lerp the lateral offset to zero over convergenceDist.
                // Direct position correction — cannot overshoot by definition.
                float t = Mathf.Clamp01(proj / convergenceDist);
                Vector3 onRay = cameraPosition + cameraDirection * proj;
                Vector3 correctedPos = onRay + initialLateralOffset * (1f - t);
                rb.position = correctedPos;
                transform.position = correctedPos;
                rb.linearVelocity = (crosshairAimPoint - correctedPos).normalized * spd;
                Physics.SyncTransforms();

                if (proj >= convergenceDist)
                {
                    hasConverged = true;
                    trailSegmentStart = correctedPos;
                    foreach (var trail in trails)
                    {
                        trail.Clear();
                        trail.emitting = true;
                    }
                }
            }
        }

        Vector3 vel = rb.linearVelocity;
        float speed = vel.magnitude;
        if (speed < 0.01f) return;

        Vector3 dir = vel / speed;
        // Spin the transform around its forward axis — trail emission point orbits
        // the centre, drawing a helix. Set trailSpinSpeed to 0 to disable.
        if (hasConverged && trailSpinSpeed != 0f)
            transform.Rotate(Vector3.forward, trailSpinSpeed * Time.fixedDeltaTime, Space.Self);
        float frameDist = speed * Time.fixedDeltaTime + 0.1f;
        float remainingDist = frameDist;
        Vector3 origin = transform.position;
        int bouncesLeft = shotData.GetProperty("bouncesLeft", 0);
        bool bounced = false;
        int portalsCrossed = 0;
        const int maxPortalsPerFrame = 4;

        while (remainingDist > 0f)
        {
            if (bounced)
            {
                transform.position = origin;
                transform.rotation = Quaternion.LookRotation(dir);
                rb.position = origin;
                rb.rotation = transform.rotation;
                rb.linearVelocity = dir * speed;
                Physics.SyncTransforms();
                bounced = false;
            }

            bool hitWall = Physics.Raycast(origin, dir, out RaycastHit wallHit,
                                           remainingDist, shotData.hitLayers);

            RaycastHit portalHit = default;
            bool hitPortal = portalsCrossed < maxPortalsPerFrame &&
                             Physics.Raycast(origin, dir, out portalHit,
                                             remainingDist, PortalLayerMask,
                                             QueryTriggerInteraction.Collide);

            Portal portal = null;
            if (hitPortal)
            {
                portal = portalHit.collider.GetComponentInParent<Portal>();
                if (portal == null || !portal.IsLinked || !portal.CanTraverse(this))
                {
                    hitPortal = false;
                    portal = null;
                }
            }

            bool portalIsCloser = hitPortal && (!hitWall || portalHit.distance <= wallHit.distance);

            if (portalIsCloser)
            {
                SpawnTrailSegment(trailSegmentStart, portalHit.point);

                transform.position = portalHit.point;
                transform.rotation = Quaternion.LookRotation(dir);
                rb.position = portalHit.point;
                rb.rotation = transform.rotation;

                var m = portal.linkedPortal.transform.localToWorldMatrix
                        * FlipMatrix
                        * portal.transform.worldToLocalMatrix
                        * transform.localToWorldMatrix;

                Teleport(portal.transform, portal.linkedPortal.transform,
                         m.GetColumn(3), m.rotation);

                vel = rb.linearVelocity;
                speed = vel.magnitude;
                dir = speed > 0.01f ? vel / speed : dir;
                origin = transform.position + dir * 0.1f;
                rb.position = origin;
                transform.position = origin;
                trailSegmentStart = origin;
                remainingDist = frameDist;
                bounced = false;
                portalsCrossed++;
                continue;
            }

            if (!hitWall) break;

            IDamageable target = wallHit.collider.GetComponentInParent<IDamageable>();

            if (target != null)
            {
                hasHit = true;
                SpawnTrailSegment(trailSegmentStart, wallHit.point);
                HitInfo info = new HitInfo(wallHit.point, wallHit.normal, wallHit.collider);
                foreach (var callback in shotData.onHitCallbacks)
                    callback.Invoke(info, shotData);
                float dmg = shotData.weaponController != null
                    ? shotData.weaponController.ApplyDamageModifier(shotData.damage, wallHit.collider)
                    : shotData.damage;
                target.TakeDamage(dmg, info);
                shotData.weaponController?.ShowHitFeedback(wallHit.collider);
                ReturnToPool();
                return;
            }

            if (!hasConverged)
            {
                Camera liveCam = Camera.main;
                bool crosshairOnThisWall = false;
                if (liveCam != null && Physics.Raycast(liveCam.transform.position,
                        liveCam.transform.forward, out RaycastHit crosshairHit,
                        maxRange, shotData.hitLayers, QueryTriggerInteraction.Ignore))
                {
                    crosshairOnThisWall = crosshairHit.collider == wallHit.collider
                        && Vector3.Distance(crosshairHit.point, wallHit.point) < 1f;
                }
                if (!crosshairOnThisWall)
                {
                    origin = wallHit.point + dir * 0.05f;
                    remainingDist -= wallHit.distance + 0.05f;
                    bounced = true;
                    if (remainingDist <= 0f) break;
                    continue;
                }
            }

            if (bouncesLeft > 0)
            {
                SpawnTrailSegment(trailSegmentStart, wallHit.point);
                trailSegmentStart = wallHit.point;

                shotData.weaponController?.SpawnDecal(wallHit.point, wallHit.normal, wallHit.collider);

                bool applyOnBounce = shotData.GetProperty("applyEffectsOnBounce", false);
                if (applyOnBounce)
                {
                    HitInfo info = new HitInfo(wallHit.point, wallHit.normal, wallHit.collider);
                    foreach (var callback in shotData.onHitCallbacks)
                        callback.Invoke(info, shotData);
                }

                Vector3 reflectInput = firstBounce ? cameraDirection : dir;
                firstBounce = false;
                dir = Vector3.Reflect(reflectInput, wallHit.normal);
                origin = wallHit.point + wallHit.normal * 0.05f;
                bouncesLeft--;
                remainingDist = frameDist;
                bounced = true;
                continue;
            }

            hasHit = true;
            SpawnTrailSegment(trailSegmentStart, wallHit.point);
            HitInfo finalInfo = new HitInfo(wallHit.point, wallHit.normal, wallHit.collider);
            shotData.weaponController?.SpawnDecal(wallHit.point, wallHit.normal, wallHit.collider);
            foreach (var callback in shotData.onHitCallbacks)
                callback.Invoke(finalInfo, shotData);
            ReturnToPool();
            return;
        }

        if (bounced)
        {
            transform.position = origin;
            transform.rotation = Quaternion.LookRotation(dir);
            rb.position = origin;
            rb.rotation = transform.rotation;
            rb.linearVelocity = dir * speed;
            Physics.SyncTransforms();
        }

        if (bouncesLeft != shotData.GetProperty("bouncesLeft", 0))
            shotData.SetProperty("bouncesLeft", bouncesLeft);
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
        Vector3 contactNormal = transform.position - contactPoint;
        contactNormal = contactNormal.sqrMagnitude < 0.0001f
            ? -rb.linearVelocity.normalized
            : contactNormal.normalized;

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

        // Detach trails BEFORE deactivating — SetActive(false) instantly wipes all
        // TrailRenderer points, so we orphan them first and let them fade on their own.
        foreach (var trail in trails)
        {
            trail.emitting = false;
            trail.transform.SetParent(null, true);
            Destroy(trail.gameObject, trail.time > 0f ? trail.time : 1f);
        }
        trails = new TrailRenderer[0];

        if (shotData != null && shotData.weaponController != null && prefabSource != null)
            shotData.weaponController.ReturnToPool(gameObject, prefabSource);
        else
            gameObject.SetActive(false);

        shotData = null;
    }
}