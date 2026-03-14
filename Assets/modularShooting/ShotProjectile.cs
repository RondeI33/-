using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ShotProjectile : MonoBehaviour
{
    [SerializeField] float lifetime = 5f;
    [SerializeField] float maxRange = 200f;

    private ShotData shotData;
    private Rigidbody rb;
    private bool hasHit;
    private float splitTotalTime;
    private float splitFireTime;
    private bool hasSplit;
    private float despawnTime;
    private GameObject prefabSource;
    private Vector3 spawnPosition;

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

        bool useGravity = data.GetProperty("useGravity", false);
        float lobAngle = data.GetProperty("lobAngle", 0f);

        rb.useGravity = useGravity;

        Vector3 velocity = data.direction.normalized * data.speed;

        if (useGravity && lobAngle > 0f)
        {
            velocity = Quaternion.AngleAxis(-lobAngle, transform.right) * velocity;
        }

        rb.linearVelocity = velocity;
    }

    void Update()
    {
        if (shotData == null) return;

        if (Time.time >= despawnTime || Vector3.Distance(transform.position, spawnPosition) >= maxRange)
        {
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

        ReturnToPool();
    }

    void FixedUpdate()
    {
        if (hasHit || shotData == null) return;

        int bouncesLeft = shotData.GetProperty("bouncesLeft", 0);
        if (bouncesLeft <= 0) return;

        Vector3 vel = rb.linearVelocity;
        float speed = vel.magnitude;
        if (speed < 0.01f) return;

        Vector3 dir = vel / speed;
        float lookAhead = speed * Time.fixedDeltaTime + 0.1f;

        if (!Physics.Raycast(transform.position, dir, out RaycastHit hit, lookAhead, shotData.hitLayers))
            return;

        if (hit.collider.GetComponentInParent<IDamageable>() != null)
            return;

        Vector3 reflected = Vector3.Reflect(dir, hit.normal);
        rb.linearVelocity = reflected * speed;
        transform.position = hit.point + hit.normal * 0.05f;

        shotData.SetProperty("bouncesLeft", bouncesLeft - 1);

        if (shotData.weaponController != null)
            shotData.weaponController.SpawnDecal(hit.point, hit.normal, hit.collider);

        bool applyOnBounce = shotData.GetProperty("applyEffectsOnBounce", false);
        if (applyOnBounce)
        {
            HitInfo info = new HitInfo(hit.point, hit.normal, hit.collider);

            foreach (var callback in shotData.onHitCallbacks)
                callback.Invoke(info, shotData);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;
        if (shotData == null) return;

        int otherLayer = 1 << other.gameObject.layer;
        if ((shotData.hitLayers.value & otherLayer) == 0) return;

        IDamageable target = other.GetComponentInParent<IDamageable>();

        if (target != null)
        {
            hasHit = true;

            Vector3 contactPoint = other.ClosestPoint(transform.position);
            Vector3 contactNormal = (transform.position - contactPoint);
            if (contactNormal.sqrMagnitude < 0.0001f)
                contactNormal = -rb.linearVelocity.normalized;
            else
                contactNormal = contactNormal.normalized;
            HitInfo info = new HitInfo(contactPoint, contactNormal, other);

            foreach (var callback in shotData.onHitCallbacks)
                callback.Invoke(info, shotData);

            target.TakeDamage(shotData.damage, info);

            if (shotData.weaponController != null)
                shotData.weaponController.ShowHitFeedback(other);

            ReturnToPool();
            return;
        }

        int bouncesLeft = shotData.GetProperty("bouncesLeft", 0);
        if (bouncesLeft > 0) return;

        hasHit = true;

        Vector3 finalPoint = other.ClosestPoint(transform.position);
        Vector3 finalNormal = (transform.position - finalPoint);
        if (finalNormal.sqrMagnitude < 0.0001f)
            finalNormal = -rb.linearVelocity.normalized;
        else
            finalNormal = finalNormal.normalized;
        HitInfo finalInfo = new HitInfo(finalPoint, finalNormal, other);

        foreach (var callback in shotData.onHitCallbacks)
            callback.Invoke(finalInfo, shotData);

        if (shotData.weaponController != null)
            shotData.weaponController.SpawnDecal(finalPoint, finalNormal, other);

        ReturnToPool();
    }

    void ReturnToPool()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (shotData != null && shotData.weaponController != null && prefabSource != null)
            shotData.weaponController.ReturnToPool(gameObject, prefabSource);
        else
            gameObject.SetActive(false);

        shotData = null;
    }
}