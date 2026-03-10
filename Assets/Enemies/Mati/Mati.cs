using UnityEngine;
using UnityEngine.AI;

public class Mati : MonoBehaviour, IEnemy
{
    [Header("References")]
    [SerializeField] private Transform swingPoint;
    [SerializeField] private Vector3 swingBoxHalfExtents = new Vector3(1f, 1f, 1.5f);
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private Transform modelTransform;

    [Header("Detection")]
    [SerializeField] private float detectionRange = 67f;
    [SerializeField] private LayerMask obstacleLayer;

    [Header("Attack")]
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private float swingDelay = 0.2f;
    [SerializeField] private float swingDuration = 0.3f;
    [SerializeField] private float minDamage = 10f;
    [SerializeField] private float maxDamage = 15f;
    [SerializeField] private float keepDistance = 1.5f;

    [Header("Health")]
    [SerializeField] private float health = 70f;

    [Header("Nav Agent")]
    [SerializeField] private float agentSpeed = 6f;
    [SerializeField] private float agentAngularSpeed = 120f;
    [SerializeField] private float agentAcceleration = 10f;
    [SerializeField] private float agentRadius = 0.72f;
    [SerializeField] private float agentHeight = 3.69f;
    [SerializeField] private float pathUpdateInterval = 0.2f;

    [Header("Animation")]
    [SerializeField] private Animator animCont;
    [SerializeField] private string swingAnimName = "MatiSwing";

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

    [Header("Walk Animation")]
    [SerializeField] private float walkBobAmount = 0.02f;
    [SerializeField] private float walkBobSpeed = 8f;
    [SerializeField] private float walkTiltAmount = 3f;
    [SerializeField] private float walkTiltSpeed = 4f;

    [Header("Sound Effects")]
    [SerializeField] private AudioSource swingAudioSource;
    [SerializeField] private AudioClip swingSound;
    [SerializeField] private AudioClip deathSound;
    [SerializeField] private float deathVolume = 1f;
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

    private NavMeshAgent agent;
    private Transform player;
    private Doors doors;
    private float attackTimer;
    private bool active;
    private float playerCenterHeight;
    private EnemyForceApplier forceApplier;
    private float swingTimer;
    private bool swingActive;
    private bool hasHitThisSwing;
    private float pathUpdateTimer;
    private bool stopped;

    public void InitAgent()
    {
        agent = gameObject.AddComponent<NavMeshAgent>();
        agent.speed = agentSpeed;
        agent.angularSpeed = agentAngularSpeed;
        agent.acceleration = agentAcceleration;
        agent.stoppingDistance = 0f;
        agent.radius = agentRadius;
        agent.height = agentHeight;
        agent.updateRotation = false;
        agent.autoBraking = false;
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

    private void UpdateWalkAnimation()
    {
        if (modelTransform == null || dying || rising || stretching || settling) return;
        if (agent == null || agent.velocity.sqrMagnitude < 0.1f)
        {
            if (wobbleTimer > hitWobbleDuration && !applyScaleOverride)
            {
                scaleOverride = modelOriginalScale;
                applyScaleOverride = false;
            }
            modelTransform.localRotation = modelOriginalRotation;
            return;
        }

        if (wobbleTimer <= hitWobbleDuration) return;

        float time = Time.time;
        float bob = Mathf.Abs(Mathf.Sin(time * walkBobSpeed)) * walkBobAmount;
        scaleOverride = modelOriginalScale + new Vector3(0f, -bob, 0f);
        applyScaleOverride = true;

        float tilt = Mathf.Sin(time * walkTiltSpeed) * walkTiltAmount;
        modelTransform.localRotation = modelOriginalRotation * Quaternion.Euler(tilt, 0f, 0f);
    }

    public void Die()
    {
        if (dying) return;

        bool lastEnemy = doors != null && doors.IsLastEnemy();
        if (KillSlowMotion.Instance != null)
            KillSlowMotion.Instance.Trigger(lastEnemy);

        dying = true;
        deathTimer = 0f;

        if (deathSound != null)
            AudioSource.PlayClipAtPoint(deathSound, transform.position, deathVolume);

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
        UpdateWalkAnimation();
        if (rising) return;

        if (!active || player == null || agent == null) return;
        if (forceApplier != null && forceApplier.IsKnocked) return;

        float dist = Vector3.Distance(transform.position, player.position);

        if (dist > detectionRange)
        {
            agent.ResetPath();
            stopped = false;
            return;
        }

        UpdateSwing();
        attackTimer -= Time.deltaTime;

        if (attackTimer <= 0f && PlayerInSwingBox())
        {
            Swing();
            attackTimer = attackCooldown;
        }

        if (dist <= keepDistance)
        {
            if (!stopped)
            {
                agent.isStopped = true;
                stopped = true;
            }
        }
        else
        {
            if (stopped)
            {
                agent.isStopped = false;
                stopped = false;
            }

            pathUpdateTimer -= Time.deltaTime;
            if (pathUpdateTimer <= 0f)
            {
                agent.SetDestination(player.position);
                pathUpdateTimer = pathUpdateInterval;
            }
        }

        if (!stopped)
            FaceMovement();
        else
            FacePlayer();
    }

    private bool PlayerInSwingBox()
    {
        if (swingPoint == null) return false;
        Collider[] hits = Physics.OverlapBox(swingPoint.position, swingBoxHalfExtents, swingPoint.rotation, playerLayer);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].CompareTag("Player"))
                return true;
        }
        return false;
    }

    private void Swing()
    {
        if (animCont != null)
            animCont.Play(swingAnimName);

        if (swingAudioSource && swingSound)
        {
            swingAudioSource.pitch = Random.Range(pitchMin, pitchMax);
            swingAudioSource.clip = swingSound;
            swingAudioSource.Play();
        }

        swingTimer = swingDelay + swingDuration;
        swingActive = false;
        hasHitThisSwing = false;
    }

    private void UpdateSwing()
    {
        if (swingTimer <= 0f) return;

        swingTimer -= Time.deltaTime;

        if (swingTimer <= swingDuration && !swingActive)
            swingActive = true;

        if (swingTimer <= 0f)
        {
            swingActive = false;
            swingTimer = 0f;
        }

        if (swingActive)
            CheckSwingHit();
    }

    private void CheckSwingHit()
    {
        if (hasHitThisSwing || swingPoint == null) return;

        Collider[] hits = Physics.OverlapBox(swingPoint.position, swingBoxHalfExtents, swingPoint.rotation, playerLayer);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].CompareTag("Player"))
            {
                PlayerHealth ph = hits[i].GetComponent<PlayerHealth>();
                if (ph != null)
                    ph.TakeDamage(Random.Range(minDamage, maxDamage));
                hasHitThisSwing = true;
                return;
            }
        }
    }

    private void FacePlayer()
    {
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * agentAngularSpeed / 10f);
    }

    private void FaceMovement()
    {
        if (agent.velocity.sqrMagnitude > 1f)
        {
            Vector3 dir = agent.velocity;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * agentAngularSpeed / 10f);
        }
        else
        {
            FacePlayer();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (swingPoint == null) return;
        Gizmos.color = swingActive ? Color.red : Color.yellow;
        Gizmos.matrix = Matrix4x4.TRS(swingPoint.position, swingPoint.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, swingBoxHalfExtents * 2f);
    }
}