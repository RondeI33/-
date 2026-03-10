using UnityEngine;

public class Atom : MonoBehaviour, IEnemy
{
    [Header("References")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform losCheckPoint;
    [SerializeField] private Transform[] spheres = new Transform[3];
    [SerializeField] private Transform modelTransform;

    [Header("Detection")]
    [SerializeField] private float detectionRange = 67f;
    [SerializeField] private float attackRange = 18f;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private float losHeightOffset = 1f;

    [Header("Shooting")]
    [SerializeField] private float burstDelay = 0.2f;
    [SerializeField] private float burstCooldown = 2.5f;
    [SerializeField] private float projectileSpeed = 20f;
    [SerializeField] private float minDamage = 4f;
    [SerializeField] private float maxDamage = 6f;
    [SerializeField] private float rechargeDelay = 0.3f;

    [Header("Health")]
    [SerializeField] private float health = 40f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float hoverHeight = 3.5f;
    [SerializeField] private float rotationSpeed = 5f;
    [SerializeField] private float stoppingDistance = 10f;
    [SerializeField] private float hoverBobSpeed = 2f;
    [SerializeField] private float hoverBobAmount = 0.3f;

    [Header("Hit Feedback")]
    [SerializeField] private GameObject hitParticlePrefab;
    [SerializeField] private float hitWobbleAngle = 9f;
    [SerializeField] private float hitWobbleSpeed = 2.33f;
    [SerializeField] private float hitWobbleDuration = 0.33f;

    [Header("Spawn Rise")]
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
    [SerializeField] private AudioSource shootAudioSource;
    [SerializeField] private AudioClip shootSound;
    [SerializeField] private AudioClip deathSound;
    [SerializeField] private float deathVolume = 1f;
    [SerializeField] private AudioClip hoverIdleSound;
    [SerializeField] private float hoverVolume = 0.5f;
    [SerializeField] private float pitchMin = 0.9f;
    [SerializeField] private float pitchMax = 1.1f;

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

    private Transform player;
    private Doors doors;
    private bool active;
    private float playerCenterHeight;
    private EnemyForceApplier forceApplier;

    private int burstIndex;
    private float burstTimer;
    private float cooldownTimer;
    private bool bursting;
    private bool recharging;
    private int rechargeIndex;
    private float rechargeTimer;
    private GameObject[] sphereVisuals;

    private AudioSource hoverAudioSource;

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

        sphereVisuals = new GameObject[spheres.Length];
        for (int i = 0; i < spheres.Length; i++)
        {
            if (spheres[i] != null)
                sphereVisuals[i] = spheres[i].gameObject;
        }

        IgnoreChildColliders();

        if (hoverIdleSound != null)
        {
            hoverAudioSource = gameObject.AddComponent<AudioSource>();
            hoverAudioSource.clip = hoverIdleSound;
            hoverAudioSource.loop = true;
            hoverAudioSource.playOnAwake = false;
            hoverAudioSource.volume = hoverVolume;
            hoverAudioSource.spatialBlend = 1f;
        }

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

    private void IgnoreChildColliders()
    {
        Collider mainCol = GetComponent<Collider>();
        if (mainCol == null) return;

        Collider[] childCols = GetComponentsInChildren<Collider>();
        for (int i = 0; i < childCols.Length; i++)
        {
            if (childCols[i] != mainCol)
                Physics.IgnoreCollision(mainCol, childCols[i]);
        }
    }

    private void SetHitboxes(bool enabled)
    {
        if (hitboxes == null) return;
        foreach (Collider col in hitboxes)
            col.enabled = enabled;
    }

    public void InitAgent() { }

    public void SetDoors(Doors room)
    {
        doors = room;
    }

    public void Activate()
    {
        active = true;
        if (hoverAudioSource != null && !hoverAudioSource.isPlaying)
            hoverAudioSource.Play();
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

        if (hoverAudioSource != null)
            hoverAudioSource.Stop();

        bool lastEnemy = doors != null && doors.IsLastEnemy();
        if (KillSlowMotion.Instance != null)
            KillSlowMotion.Instance.Trigger(lastEnemy);

        dying = true;
        deathTimer = 0f;

        if (deathSound != null)
            AudioSource.PlayClipAtPoint(deathSound, transform.position, deathVolume);

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

        if (!active || player == null) return;
        if (forceApplier != null && forceApplier.IsKnocked) return;

        float dist = Vector3.Distance(transform.position, player.position);

        if (dist > detectionRange) return;

        FacePlayer();
        Hover();

        if (recharging)
        {
            rechargeTimer -= Time.deltaTime;
            if (rechargeTimer <= 0f)
            {
                if (rechargeIndex < sphereVisuals.Length && sphereVisuals[rechargeIndex] != null)
                    sphereVisuals[rechargeIndex].SetActive(true);

                rechargeIndex++;
                if (rechargeIndex >= spheres.Length)
                {
                    recharging = false;
                    cooldownTimer = 0f;
                }
                else
                {
                    rechargeTimer = rechargeDelay;
                }
            }
            return;
        }

        bool hasLOS = CheckLineOfSight();

        if (dist <= attackRange && hasLOS)
        {
            if (bursting)
            {
                burstTimer -= Time.deltaTime;
                if (burstTimer <= 0f)
                    FireNextSphere();
            }
            else
            {
                cooldownTimer -= Time.deltaTime;
                if (cooldownTimer <= 0f)
                    StartBurst();
            }
        }
        else
        {
            MoveTowardPlayer();
        }
    }

    private void Hover()
    {
        float bobOffset = Mathf.Sin(Time.time * hoverBobSpeed) * hoverBobAmount;
        Vector3 pos = transform.position;

        float targetY = pos.y;
        if (Physics.Raycast(transform.position + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 50f, obstacleLayer))
            targetY = hit.point.y + hoverHeight + bobOffset;

        if (Physics.Raycast(transform.position, Vector3.up, out RaycastHit ceilingHit, 10f, obstacleLayer))
            targetY = Mathf.Min(targetY, ceilingHit.point.y - 1f);

        pos.y = Mathf.Lerp(pos.y, targetY, Time.deltaTime * 3f);
        transform.position = pos;
    }

    private void MoveTowardPlayer()
    {
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.magnitude > stoppingDistance)
            transform.position += dir.normalized * moveSpeed * Time.deltaTime;
    }

    private void FacePlayer()
    {
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * rotationSpeed);
    }

    private void StartBurst()
    {
        bursting = true;
        burstIndex = 0;
        FireNextSphere();
    }

    private void FireNextSphere()
    {
        if (burstIndex >= spheres.Length)
        {
            bursting = false;
            recharging = true;
            rechargeIndex = 0;
            rechargeTimer = burstCooldown;
            return;
        }

        if (spheres[burstIndex] != null && sphereVisuals[burstIndex] != null)
        {
            Vector3 origin = spheres[burstIndex].position;
            Vector3 target = player.position + Vector3.up * playerCenterHeight;
            Vector3 dir = (target - origin).normalized;

            if (shootAudioSource && shootSound)
            {
                shootAudioSource.pitch = Random.Range(pitchMin, pitchMax);
                shootAudioSource.clip = shootSound;
                shootAudioSource.Play();
            }

            GameObject proj = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(dir));
            Rigidbody projRb = proj.GetComponent<Rigidbody>();
            if (projRb != null)
                projRb.linearVelocity = dir * projectileSpeed;

            Projectajl p = proj.GetComponent<Projectajl>();
            if (p != null)
                p.damage = Random.Range(minDamage, maxDamage);

            sphereVisuals[burstIndex].SetActive(false);
        }

        burstIndex++;
        burstTimer = burstDelay;
    }

    private bool CheckLineOfSight()
    {
        Vector3 origin = losCheckPoint != null ? losCheckPoint.position : transform.position + Vector3.up * losHeightOffset;
        Vector3 target = player.position + Vector3.up * playerCenterHeight;
        Vector3 dir = target - origin;
        return !Physics.Raycast(origin, dir.normalized, dir.magnitude, obstacleLayer);
    }
}