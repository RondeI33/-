using UnityEngine;
using UnityEngine.AI;

public class Telewizjołeb : MonoBehaviour, IEnemy
{
    [Header("References")]
    [SerializeField] private Transform shootPoint;
    [SerializeField] private Transform hand;
    [SerializeField] private Transform losCheckPoint;
    [SerializeField] private Transform modelTransform;

    [Header("Detection")]
    [SerializeField] private float detectionRange = 67f;
    [SerializeField] private float attackRange = 67f;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private float losHeightOffset = 1f;

    [Header("Shooting")]
    [SerializeField] private float aimDuration = 1.5f;
    [SerializeField] private float fireCooldown = 3f;
    [SerializeField] private float minDamage = 15f;
    [SerializeField] private float maxDamage = 20f;
    [SerializeField] private float lockBeforeShot = 0.33f;

    [Header("Laser Sight")]
    [SerializeField] private float laserWidth = 0.03f;
    [SerializeField] private Color laserColor = Color.red;
    [SerializeField] private Color lockColor = Color.white;
    [SerializeField] private Color trailColor = Color.yellow;
    [SerializeField] private float trailExtraLength = 10f;
    [SerializeField] private float laserFadeDuration = 0.8f;
    [SerializeField] private float losGracePeriod = 0.5f;
    [SerializeField] private float flickerSpeed = 20f;

    [Header("Hand Aim")]
    [SerializeField] private Vector3 handAimOffset = new Vector3(-90f, 90f, 0f);

    [Header("Retreat")]
    [SerializeField] private float retreatDistance = 15f;
    [SerializeField] private float retreatSpeed = 6f;

    [Header("Health")]
    [SerializeField] private float health = 35f;

    [Header("Nav Agent")]
    [SerializeField] private float agentSpeed = 4.67f;
    [SerializeField] private float agentAngularSpeed = 120f;
    [SerializeField] private float agentAcceleration = 8f;
    [SerializeField] private float agentStoppingDistance = 0.33f;
    [SerializeField] private float agentRadius = 0.72f;
    [SerializeField] private float agentHeight = 3.69f;

    [Header("Animation")]
    [SerializeField] private Animator animCont;

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
    [SerializeField] private AudioSource shootAudioSource;
    [SerializeField] private AudioClip shootSound;
    [SerializeField] private AudioClip chargingSound;
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

    private LineRenderer laserLine;
    private enum State { Approaching, Aiming, Retreating }

    private NavMeshAgent agent;
    private Transform player;
    private Doors doors;
    private bool active;
    private float playerCenterHeight;
    private Material laserMat;
    private Material lockMat;
    private EnemyForceApplier forceApplier;

    private State currentState;
    private float aimTimer;
    private float cooldownTimer;
    private bool hasRetreatTarget;
    private float losLostTimer;
    private Vector3 lockedTarget;
    private bool targetLocked;
    private GameObject activeTrail;

    private AudioSource chargingAudioSource;

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

        currentState = State.Approaching;

        if (chargingSound != null)
        {
            chargingAudioSource = gameObject.AddComponent<AudioSource>();
            chargingAudioSource.clip = chargingSound;
            chargingAudioSource.loop = true;
            chargingAudioSource.playOnAwake = false;
            chargingAudioSource.spatialBlend = 0f;
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

    private Material CreateLaserMaterial(Color color)
    {
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = color;
        return mat;
    }

    public void SetDoors(Doors room)
    {
        doors = room;
    }

    public void Activate()
    {
        active = true;

        laserMat = CreateLaserMaterial(laserColor);
        lockMat = CreateLaserMaterial(lockColor);

        GameObject laserObj = new GameObject("Laser");
        laserObj.transform.SetParent(transform);
        laserLine = laserObj.AddComponent<LineRenderer>();
        laserLine.startWidth = laserWidth;
        laserLine.endWidth = laserWidth;
        laserLine.positionCount = 2;
        laserLine.useWorldSpace = true;
        laserLine.material = laserMat;
        laserLine.startColor = laserColor;
        laserLine.endColor = laserColor;
        laserLine.enabled = false;
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

        StopChargingSound();

        if (activeTrail != null) { Destroy(activeTrail); activeTrail = null; }

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

    private void StartChargingSound()
    {
        if (chargingAudioSource != null && !chargingAudioSource.isPlaying)
        {
            chargingAudioSource.pitch = Random.Range(pitchMin, pitchMax);
            chargingAudioSource.Play();
        }
    }

    private void StopChargingSound()
    {
        if (chargingAudioSource != null && chargingAudioSource.isPlaying)
            chargingAudioSource.Stop();
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
            DisableLaser();
            StopChargingSound();
            if (activeTrail != null) { Destroy(activeTrail); activeTrail = null; }
            currentState = State.Approaching;
            return;
        }

        switch (currentState)
        {
            case State.Approaching:
                HandleApproaching(dist);
                break;
            case State.Aiming:
                HandleAiming(dist);
                break;
            case State.Retreating:
                HandleRetreating(dist);
                break;
        }
    }

    private void HandleApproaching(float dist)
    {
        if (laserLine != null && laserLine.enabled)
            DisableLaser();
        if (activeTrail != null) { Destroy(activeTrail); activeTrail = null; }
        StopChargingSound();

        bool hasLOS = CheckLineOfSight();

        if (dist <= attackRange && hasLOS)
        {
            agent.ResetPath();
            currentState = State.Aiming;
            aimTimer = aimDuration;
            losLostTimer = 0f;
            targetLocked = false;
            StartChargingSound();
            return;
        }

        FaceMovement();
        AimHand();
        agent.speed = agentSpeed;
        agent.SetDestination(player.position);
    }

    private void HandleAiming(float dist)
    {
        bool hasLOS = CheckLineOfSight();

        if (!hasLOS || dist > attackRange)
        {
            losLostTimer += Time.deltaTime;
            if (losLostTimer >= losGracePeriod)
            {
                DisableLaser();
                StopChargingSound();
                if (activeTrail != null) { Destroy(activeTrail); activeTrail = null; }
                targetLocked = false;
                aimTimer = aimDuration;
                currentState = State.Approaching;
                return;
            }
        }
        else
        {
            losLostTimer = 0f;
        }

        agent.ResetPath();

        if (!targetLocked && aimTimer <= lockBeforeShot)
        {
            lockedTarget = player.position + Vector3.up * playerCenterHeight;
            targetLocked = true;
            laserLine.material = lockMat;
        }

        if (!targetLocked)
        {
            FacePlayer();
            AimHand();
        }

        float progress = 1f - (aimTimer / aimDuration);
        float currentWidth = Mathf.Lerp(laserWidth, laserWidth * 3.33f, progress);

        if (targetLocked)
        {
            float flicker = (Mathf.Sin(Time.time * flickerSpeed) + 1f) * 0.5f;
            Color flickerColor = new Color(lockColor.r, lockColor.g, lockColor.b, Mathf.Lerp(0.2f, 1f, flicker));
            laserLine.startColor = flickerColor;
            laserLine.endColor = flickerColor;
            UpdateLaserLocked(currentWidth);
        }
        else
        {
            laserLine.startColor = laserColor;
            laserLine.endColor = laserColor;
            UpdateLaser(currentWidth);
        }

        aimTimer -= Time.deltaTime;
        if (aimTimer <= 0f)
        {
            StopChargingSound();

            Vector3 laserStart = shootPoint != null ? shootPoint.position : transform.position + Vector3.up * losHeightOffset;
            SpawnFadingLaser(laserStart, lockedTarget);
            Shoot();

            DisableLaser();
            targetLocked = false;
            cooldownTimer = fireCooldown;
            hasRetreatTarget = false;
            currentState = State.Retreating;
        }
    }

    private void HandleRetreating(float dist)
    {
        if (laserLine != null && laserLine.enabled)
            DisableLaser();
        StopChargingSound();
        cooldownTimer -= Time.deltaTime;

        AimHand();

        if (!hasRetreatTarget || (agent.hasPath && agent.remainingDistance < 0.5f))
        {
            hasRetreatTarget = false;
            Retreat();
        }

        FaceMovement();

        if (cooldownTimer <= 0f)
        {
            currentState = State.Approaching;
            hasRetreatTarget = false;
        }
    }

    private void Retreat()
    {
        if (hasRetreatTarget) return;

        Vector3 dirAway = (transform.position - player.position).normalized;
        NavMeshPath path = new NavMeshPath();

        Vector3 retreatTarget = transform.position + dirAway * retreatDistance;
        if (agent.CalculatePath(retreatTarget, path) && path.status == NavMeshPathStatus.PathComplete)
        {
            agent.speed = retreatSpeed;
            agent.SetPath(path);
            hasRetreatTarget = true;
            return;
        }

        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f;
            Vector3 rotatedDir = Quaternion.Euler(0f, angle, 0f) * dirAway;
            retreatTarget = transform.position + rotatedDir * retreatDistance;

            if (agent.CalculatePath(retreatTarget, path) && path.status == NavMeshPathStatus.PathComplete)
            {
                agent.speed = retreatSpeed;
                agent.SetPath(path);
                hasRetreatTarget = true;
                return;
            }
        }

        hasRetreatTarget = true;
    }

    private void SpawnFadingLaser(Vector3 start, Vector3 end)
    {
        Vector3 dir = (end - start).normalized;
        Vector3 extendedEnd = end + dir * trailExtraLength;
        float finalWidth = laserWidth * 3.33f;

        GameObject trailObj = new GameObject("LaserTrail");
        LineRenderer trail = trailObj.AddComponent<LineRenderer>();
        trail.startWidth = finalWidth;
        trail.endWidth = finalWidth;
        trail.positionCount = 2;
        trail.SetPosition(0, start);
        trail.SetPosition(1, extendedEnd);
        trail.useWorldSpace = true;
        trail.material = CreateLaserMaterial(trailColor);
        trail.startColor = trailColor;
        trail.endColor = trailColor;

        LaserTrailFade fade = trailObj.AddComponent<LaserTrailFade>();
        fade.Init(trail, trailColor, laserFadeDuration);

        activeTrail = trailObj;
    }

    private void UpdateLaser(float width)
    {
        if (laserLine == null) return;

        Vector3 origin = shootPoint != null ? shootPoint.position : transform.position + Vector3.up * losHeightOffset;
        Vector3 target = player.position + Vector3.up * playerCenterHeight;

        laserLine.startWidth = width;
        laserLine.endWidth = width;
        laserLine.enabled = true;
        laserLine.SetPosition(0, origin);
        laserLine.SetPosition(1, target);
    }

    private void UpdateLaserLocked(float width)
    {
        if (laserLine == null) return;

        Vector3 origin = shootPoint != null ? shootPoint.position : transform.position + Vector3.up * losHeightOffset;
        Vector3 dir = (lockedTarget - origin).normalized;
        Vector3 extendedEnd = lockedTarget + dir * trailExtraLength;

        laserLine.startWidth = width;
        laserLine.endWidth = width;
        laserLine.enabled = true;
        laserLine.SetPosition(0, origin);
        laserLine.SetPosition(1, extendedEnd);
    }

    private void DisableLaser()
    {
        if (laserLine != null)
        {
            laserLine.enabled = false;
            laserLine.startColor = laserColor;
            laserLine.endColor = laserColor;
            laserLine.material = laserMat;
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
        if (agent.hasPath && agent.velocity.sqrMagnitude > 0.1f)
        {
            Vector3 dir = agent.velocity;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * agentAngularSpeed / 10f);
        }
    }

    private bool CheckLineOfSight()
    {
        Vector3 origin = transform.position + Vector3.up * losHeightOffset;
        Vector3 target = player.position + Vector3.up * playerCenterHeight;
        Vector3 dir = target - origin;
        return !Physics.Raycast(origin, dir.normalized, dir.magnitude, obstacleLayer);
    }

    private void AimHand()
    {
        if (hand == null) return;
        Vector3 target = player.position + Vector3.up * playerCenterHeight;
        Vector3 dir = target - hand.position;
        if (dir.sqrMagnitude > 0.001f)
            hand.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(handAimOffset);
    }

    private void Shoot()
    {
        Vector3 origin = shootPoint != null ? shootPoint.position : transform.position + Vector3.up * losHeightOffset;
        Vector3 dir = (lockedTarget - origin).normalized;

        Debug.DrawRay(origin, dir * attackRange, Color.yellow, 2f);

        animCont.Play("Telewizjołeb_Shoot", -1, 0f);

        if (shootAudioSource && shootSound)
        {
            shootAudioSource.pitch = Random.Range(pitchMin, pitchMax);
            shootAudioSource.clip = shootSound;
            shootAudioSource.Play();
        }

        RaycastHit[] hits = Physics.RaycastAll(origin, dir, attackRange);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.CompareTag("Player"))
            {
                PlayerHealth ph = hit.collider.GetComponent<PlayerHealth>();
                if (ph != null)
                    ph.TakeDamage(Random.Range(minDamage, maxDamage));
                return;
            }
        }
    }
}