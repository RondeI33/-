using UnityEngine;
using UnityEngine.AI;

public class Remo : MonoBehaviour, IEnemy
{
    [Header("Detection")]
    [SerializeField] private float detectionRange = 67f;
    [SerializeField] private float explodeRange = 3f;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private float losHeightOffset = 1f;

    [Header("Explosion")]
    [SerializeField] private float explosionRadius = 6f;
    [SerializeField] private float minDamage = 20f;
    [SerializeField] private float maxDamage = 30f;
    [SerializeField] private float minEnemyDamage = 15f;
    [SerializeField] private float maxEnemyDamage = 25f;
    [SerializeField] private float knockbackForce = 30f;
    [SerializeField] private float growDuration = 0.5f;
    [SerializeField] private float growScale = 2.5f;
    [SerializeField] private float enemyKnockbackForce = 20f;
    [SerializeField] private GameObject explosionEffectPrefab;
    [SerializeField] private LayerMask playerLayer;

    [Header("Rolling Visual")]
    [SerializeField] private Transform ballVisual;
    [SerializeField] private float ballRadius = 0.5f;

    [Header("Health")]
    [SerializeField] private float health = 30f;

    [Header("Nav Agent")]
    [SerializeField] private float agentSpeed = 6f;
    [SerializeField] private float agentAngularSpeed = 200f;
    [SerializeField] private float agentAcceleration = 12f;
    [SerializeField] private float agentStoppingDistance = 0.1f;
    [SerializeField] private float agentRadius = 0.72f;
    [SerializeField] private float agentHeight = 1.5f;

    [Header("Hit Feedback")]
    [SerializeField] private GameObject hitParticlePrefab;
    [SerializeField] private float hitWobbleAngle = 9f;
    [SerializeField] private float hitWobbleSpeed = 2.33f;
    [SerializeField] private float hitWobbleDuration = 0.33f;

    [Header("Spawn Rise")]
    [SerializeField] private Transform modelTransform;
    [SerializeField] private float riseDepth = 2.133f;
    [SerializeField] private float riseDepthRandomRange = 0.133f;
    [SerializeField] private float riseDuration = 0.33f;
    [SerializeField] private float riseDurationRandomRange = 0f;
    [SerializeField] private float hitboxEnableTime = 0.13f;

    [Header("Spawn Stretch")]
    [SerializeField] private float stretchAmount = 0.13f;
    [SerializeField] private float stretchDuration = 0.23f;
    [SerializeField] private float stretchStartPercent = 0f;
    [SerializeField] private float settleDuration = 0.23f;

    [Header("Death Effect")]
    [SerializeField] private Shader deathShader;
    [SerializeField] private float deathDuration = 0.8f;
    [SerializeField] private float deathRiseHeight = 1.5f;
    [SerializeField] private float deathStretchY = 2f;
    [SerializeField] private float deathShrinkXZ = 0.05f;
    [SerializeField] private Color deathColor = new Color(1f, 0.85f, 0.3f, 1f);

    [Header("Sound Effects")]
    [SerializeField] private AudioClip explosionSound;
    [SerializeField] private float explosionVolume = 1f;

    private bool dying;
    private float deathTimer;
    private Vector3 deathStartPos;
    private Vector3 deathStartScale;
    private Material[] deathMaterials;
    private Color[] originalColors;
    private Shader unlitShader;

    private float wobbleTimer;
    private Vector3 modelOriginalScale;
    private Vector3 scaleOverride;
    private bool applyScaleOverride;

    private bool rising;
    private float riseTimer;
    private float actualRiseDepth;
    private float actualRiseDuration;
    private Vector3 modelStartLocalPos;
    private Vector3 modelTargetLocalPos;
    private bool hitboxesEnabled;
    private Collider[] hitboxes;

    private bool stretching;
    private float stretchTimer;
    private bool settling;
    private float settleTimer;
    private Quaternion modelOriginalRotation;

    private NavMeshAgent agent;
    private Transform player;
    private Doors doors;
    private bool active;
    private float playerCenterHeight;
    private Vector3 lastPosition;
    private bool exploding;
    private float growTimer;
    private Vector3 originalScale;
    private EnemyForceApplier forceApplier;

    public void InitAgent()
    {
        agent = gameObject.AddComponent<NavMeshAgent>();
        agent.speed = agentSpeed;
        agent.angularSpeed = agentAngularSpeed;
        agent.acceleration = agentAcceleration;
        agent.stoppingDistance = agentStoppingDistance;
        agent.radius = agentRadius;
        agent.height = agentHeight;
        agent.updateRotation = false;
    }

    private void Start()
    {
        forceApplier = GetComponentInChildren<EnemyForceApplier>();
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            CharacterController cc = playerObj.GetComponent<CharacterController>();
            if (cc != null)
                playerCenterHeight = cc.center.y + 0.33f;
        }

        lastPosition = transform.position;
        originalScale = transform.localScale;

        if (modelTransform != null)
        {
            modelOriginalScale = modelTransform.localScale;
            modelOriginalRotation = modelTransform.localRotation;
            modelTargetLocalPos = modelTransform.localPosition;

            actualRiseDepth = riseDepth + Random.Range(-riseDepthRandomRange, riseDepthRandomRange);
            actualRiseDuration = riseDuration + Random.Range(-riseDurationRandomRange, riseDurationRandomRange);

            modelStartLocalPos = modelTargetLocalPos + Vector3.down * actualRiseDepth;
            modelTransform.localPosition = modelStartLocalPos;
        }

        hitboxes = modelTransform != null
            ? modelTransform.GetComponentsInChildren<Collider>()
            : GetComponentsInChildren<Collider>();

        SetHitboxes(false);
        rising = true;
        riseTimer = 0f;
        hitboxesEnabled = false;
        wobbleTimer = hitWobbleDuration + 1f;
        applyScaleOverride = false;
    }

    private void LateUpdate()
    {
        if (modelTransform != null && applyScaleOverride)
            modelTransform.localScale = scaleOverride;
    }

    private void SetHitboxes(bool enabled)
    {
        if (hitboxes == null) return;
        foreach (Collider col in hitboxes)
            col.enabled = enabled;
    }

    public void SetDoors(Doors room)
    {
        doors = room;
    }

    public void Activate()
    {
        active = true;
    }

    public void TakeDamage(float damage)
    {
        if (!hitboxesEnabled) return;
        health -= damage;
        TriggerWobble();
        if (health <= 0f)
            Die();
    }

    public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (!hitboxesEnabled) return;
        SpawnHitParticle(hitPoint, hitNormal);
        TakeDamage(damage);
    }

    private void SpawnHitParticle(Vector3 hitPoint, Vector3 hitNormal)
    {
        if (hitParticlePrefab == null) return;
        Quaternion rot = hitNormal.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(hitNormal)
            : Quaternion.identity;
        GameObject fx = Instantiate(hitParticlePrefab, hitPoint, rot);
        Destroy(fx, 3f);
    }

    private void TriggerWobble()
    {
        if (modelTransform == null) return;
        wobbleTimer = 0f;
    }

    private void UpdateHitWobble()
    {
        if (modelTransform == null || wobbleTimer > hitWobbleDuration) return;

        wobbleTimer += Time.deltaTime;
        float progress = wobbleTimer / hitWobbleDuration;
        float decay = 1f - progress;
        float wave = Mathf.Sin(progress * hitWobbleSpeed * Mathf.PI);
        float scaleOffset = wave * decay * hitWobbleAngle * 0.01f;

        scaleOverride = modelOriginalScale + new Vector3(0f, 0f, scaleOffset);
        applyScaleOverride = true;

        if (wobbleTimer >= hitWobbleDuration)
            applyScaleOverride = false;
    }

    private void UpdateRise()
    {
        if (!rising || modelTransform == null) return;

        riseTimer += Time.deltaTime;

        if (!hitboxesEnabled && riseTimer >= actualRiseDuration - hitboxEnableTime)
        {
            SetHitboxes(true);
            hitboxesEnabled = true;
        }

        float t = Mathf.Clamp01(riseTimer / actualRiseDuration);
        float eased = t * t * (3f - 2f * t);
        modelTransform.localPosition = Vector3.Lerp(modelStartLocalPos, modelTargetLocalPos, eased);

        float stretchStartTime = actualRiseDuration * stretchStartPercent;
        if (riseTimer > stretchStartTime)
        {
            float stretchT = Mathf.Clamp01((riseTimer - stretchStartTime) / stretchDuration);
            float stretchEased = stretchT * stretchT;
            float peakY = modelOriginalScale.y + stretchAmount;
            scaleOverride = new Vector3(modelOriginalScale.x, Mathf.Lerp(modelOriginalScale.y, peakY, stretchEased), modelOriginalScale.z);
            applyScaleOverride = true;
        }

        if (t >= 1f)
        {
            modelTransform.localPosition = modelTargetLocalPos;
            rising = false;

            float elapsed = riseTimer - actualRiseDuration * stretchStartPercent;
            if (elapsed >= stretchDuration)
            {
                settling = true;
                settleTimer = 0f;
            }
            else
            {
                stretching = true;
                stretchTimer = elapsed;
            }
        }
    }

    private void UpdateStretch()
    {
        if (!stretching || modelTransform == null) return;

        stretchTimer += Time.deltaTime;
        float t = Mathf.Clamp01(stretchTimer / stretchDuration);
        float eased = t * t;
        float peakY = modelOriginalScale.y + stretchAmount;
        scaleOverride = new Vector3(modelOriginalScale.x, Mathf.Lerp(modelOriginalScale.y, peakY, eased), modelOriginalScale.z);
        applyScaleOverride = true;

        if (t >= 1f)
        {
            scaleOverride = new Vector3(modelOriginalScale.x, peakY, modelOriginalScale.z);
            stretching = false;
            settling = true;
            settleTimer = 0f;
        }
    }

    private void UpdateSettle()
    {
        if (!settling || modelTransform == null) return;

        settleTimer += Time.deltaTime;
        float progress = settleTimer / settleDuration;

        if (progress >= 1f)
        {
            applyScaleOverride = false;
            settling = false;
            return;
        }

        float peakY = modelOriginalScale.y + stretchAmount;
        float eased = progress * progress * (3f - 2f * progress);
        float yScale = Mathf.Lerp(peakY, modelOriginalScale.y, eased);
        scaleOverride = new Vector3(modelOriginalScale.x, yScale, modelOriginalScale.z);
        applyScaleOverride = true;
    }

    public void Die()
    {
        if (dying) return;

        bool lastEnemy = doors != null && doors.IsLastEnemy();
        if (KillSlowMotion.Instance != null)
            KillSlowMotion.Instance.Trigger(lastEnemy);

        dying = true;
        deathTimer = 0f;

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
        }
        if (agent != null)
        {
            agent.enabled = false;
        }

        SetHitboxes(false);

        if (modelTransform != null)
        {
            deathStartPos = modelTransform.localPosition;
            deathStartScale = modelTransform.localScale;
        }

        Renderer[] renderers = modelTransform != null
            ? modelTransform.GetComponentsInChildren<Renderer>()
            : GetComponentsInChildren<Renderer>();

        unlitShader = deathShader;

        var matList = new System.Collections.Generic.List<Material>();
        var colorList = new System.Collections.Generic.List<Color>();

        foreach (Renderer r in renderers)
        {
            foreach (Material m in r.materials)
            {
                matList.Add(m);
                colorList.Add(m.color);
                if (unlitShader != null)
                    m.shader = unlitShader;
            }
        }

        deathMaterials = matList.ToArray();
        originalColors = colorList.ToArray();
    }

    private void UpdateDeath()
    {
        if (!dying || modelTransform == null) return;

        deathTimer += Time.deltaTime;
        float t = Mathf.Clamp01(deathTimer / deathDuration);
        float eased = t * t;

        float yScale = Mathf.Lerp(deathStartScale.y, deathStartScale.y * deathStretchY, eased);
        float xScale = Mathf.Lerp(deathStartScale.x, deathShrinkXZ, eased);
        float zScale = Mathf.Lerp(deathStartScale.z, deathShrinkXZ, eased);
        scaleOverride = new Vector3(xScale, yScale, zScale);
        applyScaleOverride = true;

        modelTransform.localPosition = deathStartPos + Vector3.up * deathRiseHeight * eased;

        Color targetColor = Color.Lerp(deathColor, Color.white, eased);
        if (deathMaterials != null)
        {
            for (int i = 0; i < deathMaterials.Length; i++)
            {
                if (deathMaterials[i] != null)
                    deathMaterials[i].color = targetColor;
            }
        }

        if (t >= 1f)
        {
            if (doors != null)
                doors.OnEnemyDied(this);
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (dying)
        {
            UpdateDeath();
            return;
        }

        UpdateRise();
        UpdateStretch();
        UpdateSettle();
        UpdateHitWobble();
        if (rising) return;

        if (!active || player == null || agent == null || !agent.enabled) return;
        if (forceApplier != null && forceApplier.IsKnocked) return;

        if (exploding)
        {
            growTimer += Time.deltaTime;
            float t = Mathf.Clamp01(growTimer / growDuration);
            transform.localScale = Vector3.Lerp(originalScale, originalScale * growScale, t);

            if (t >= 1f)
                Explode();

            return;
        }

        float dist = Vector3.Distance(transform.position, player.position);

        if (dist > detectionRange)
        {
            agent.ResetPath();
            return;
        }

        if (dist <= explodeRange && CheckLineOfSight())
        {
            StartExploding();
            return;
        }

        agent.SetDestination(player.position);
        RollVisual();
    }

    private void RollVisual()
    {
        Transform target = ballVisual != null ? ballVisual : transform;

        Vector3 movement = transform.position - lastPosition;
        movement.y = 0f;
        float distance = movement.magnitude;

        if (distance > 0.001f)
        {
            Vector3 rotationAxis = Vector3.Cross(Vector3.up, movement.normalized);
            float angle = (distance / ballRadius) * Mathf.Rad2Deg;
            target.Rotate(rotationAxis, angle, Space.World);
        }

        lastPosition = transform.position;
    }

    private bool CheckLineOfSight()
    {
        Vector3 origin = transform.position + Vector3.up * losHeightOffset;
        Vector3 targetPos = player.position + Vector3.up * playerCenterHeight;
        Vector3 dir = targetPos - origin;

        return !Physics.Raycast(origin, dir.normalized, dir.magnitude, obstacleLayer);
    }

    private void StartExploding()
    {
        exploding = true;
        growTimer = 0f;
        agent.ResetPath();
        agent.isStopped = true;
    }

    private void Explode()
    {
        if (explosionEffectPrefab != null)
            Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);

        if (explosionSound != null)
            AudioSource.PlayClipAtPoint(explosionSound, transform.position, explosionVolume);

        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);
        System.Collections.Generic.HashSet<Transform> alreadyHit = new System.Collections.Generic.HashSet<Transform>();

        foreach (Collider hit in hits)
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;

            PlayerHealth ph = hit.GetComponentInParent<PlayerHealth>();
            if (ph != null && alreadyHit.Add(ph.transform))
            {
                float dist = Vector3.Distance(transform.position, ph.transform.position);
                float falloff = 1f - Mathf.Clamp01(dist / explosionRadius);
                Vector3 dir = (ph.transform.position - transform.position).normalized;
                dir.y = Mathf.Max(dir.y, 0.3f);
                dir.Normalize();
                ph.TakeDamage(Mathf.Lerp(minDamage, maxDamage, falloff));

                ForceApplier fa = ph.GetComponent<ForceApplier>();
                if (fa != null)
                    fa.AddForce(dir * knockbackForce * falloff, ForceMode.Impulse);
            }

            EnemyForceApplier efa = hit.GetComponentInParent<EnemyForceApplier>();
            if (efa != null && alreadyHit.Add(efa.transform))
            {
                float dist = Vector3.Distance(transform.position, efa.transform.position);
                float falloff = 1f - Mathf.Clamp01(dist / explosionRadius);
                Vector3 dir = (efa.transform.position - transform.position).normalized;
                dir.y = 0f;
                dir.Normalize();
                efa.AddForce(dir * enemyKnockbackForce * falloff, ForceMode.Impulse);

                IEnemy enemy = efa.GetComponentInParent<IEnemy>();
                if (enemy != null)
                    enemy.TakeDamage(Mathf.Lerp(minEnemyDamage, maxEnemyDamage, falloff));
            }
        }

        Die();
    }
}