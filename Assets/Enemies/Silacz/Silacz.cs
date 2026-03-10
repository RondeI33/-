using UnityEngine;
using UnityEngine.AI;

public class Silacz : MonoBehaviour, IEnemy
{
    [Header("References")]
    [SerializeField] private Transform shootPoint;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform hand;
    [SerializeField] private Transform losCheckPoint;
    [SerializeField] private Transform modelTransform;

    [Header("Detection")]
    [SerializeField] private float detectionRange = 67f;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private float losHeightOffset = 1f;

    [Header("Shooting")]
    [SerializeField] private float fireRate = 3f;
    [SerializeField] private float projectileSpeed = 20f;
    [SerializeField] private float minDamage = 4f;
    [SerializeField] private float maxDamage = 6f;

    [Header("Ammo")]
    [SerializeField] private int maxAmmo = 8;
    [SerializeField] private float reloadCooldown = 4f;

    [Header("Health")]
    [SerializeField] private float health = 70f;

    [Header("Wander")]
    [SerializeField] private float wanderRadius = 15f;
    [SerializeField] private float rotateToPlayerSpeed = 5f;

    [Header("Aiming")]
    [SerializeField] private float aimUpAngle = 25f;
    [SerializeField] private float aimDownAngle = 40f;
    [SerializeField] private float aimDownDuration = 0.6f;
    [SerializeField] private float preAimPause = 0.3f;
    [SerializeField] private float handTransitionSpeed = 5f;
    [SerializeField] private Vector3 handRotationOffset = new Vector3(-90f, 90f, 0f);

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

    [Header("Spread")]
    [SerializeField] private float spreadAngle = 5f;

    [Header("Sound Effects")]
    [SerializeField] private AudioSource shootAudioSource;
    [SerializeField] private AudioClip shootSound;
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

    private enum State { Wandering, CheckingLOS, AimingDown, Shooting, Reloading }

    private NavMeshAgent agent;
    private Transform player;
    private Doors doors;
    private float fireTimer;
    private bool active;
    private float playerCenterHeight;
    private EnemyForceApplier forceApplier;

    private State currentState = State.Wandering;
    private int currentAmmo;
    private float reloadTimer;
    private Vector3 lockedShootDirection;
    private Vector3 raisedShootDirection;
    private bool hasWanderTarget;
    private float aimDownTimer;
    private float preAimTimer;

    private Quaternion currentHandRotation;
    private Quaternion targetHandRotation;

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
        currentAmmo = maxAmmo;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            CharacterController cc = playerObj.GetComponent<CharacterController>();
            if (cc != null)
                playerCenterHeight = cc.center.y + 0.33f;
        }

        if (hand != null)
            currentHandRotation = hand.rotation;

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

        if (hand == null) return;
        currentHandRotation = Quaternion.Slerp(currentHandRotation, targetHandRotation, Time.deltaTime * handTransitionSpeed);
        hand.rotation = currentHandRotation;
    }

    private void SetHitboxes(bool enabled)
    {
        if (hitboxes == null) return;
        foreach (Collider col in hitboxes)
            col.enabled = enabled;
    }

    public void SetDoors(Doors room) => doors = room;
    public void Activate() => active = true;

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
            currentState = State.Wandering;
            hasWanderTarget = false;
            return;
        }

        switch (currentState)
        {
            case State.Wandering:
                HandleWandering();
                break;
            case State.CheckingLOS:
                HandleCheckingLOS();
                break;
            case State.AimingDown:
                HandleAimingDown();
                break;
            case State.Shooting:
                HandleShooting();
                break;
            case State.Reloading:
                HandleReloading();
                break;
        }
    }

    private Quaternion GetHandRotationForDirection(Vector3 dir)
    {
        return Quaternion.LookRotation(dir) * Quaternion.Euler(handRotationOffset);
    }

    private Vector3 GetRaisedDirection()
    {
        return Quaternion.AngleAxis(-aimUpAngle, transform.right) * transform.forward;
    }

    private Vector3 GetLoweredDirection()
    {
        return Quaternion.AngleAxis(aimDownAngle, transform.right) * transform.forward;
    }

    private void HandleWandering()
    {
        targetHandRotation = GetHandRotationForDirection(GetLoweredDirection());

        if (!hasWanderTarget || ReachedDestination())
        {
            if (ReachedDestination() && hasWanderTarget)
            {
                agent.ResetPath();
                currentState = State.CheckingLOS;
                return;
            }

            PickRandomWanderPoint();
        }

        FaceMovement();
    }

    private void HandleCheckingLOS()
    {
        targetHandRotation = GetHandRotationForDirection(GetRaisedDirection());

        Vector3 dirToPlayer = player.position - transform.position;
        dirToPlayer.y = 0f;

        if (dirToPlayer.sqrMagnitude < 0.001f)
        {
            currentState = State.Wandering;
            hasWanderTarget = false;
            return;
        }

        Quaternion targetRot = Quaternion.LookRotation(dirToPlayer);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotateToPlayerSpeed);

        float angle = Quaternion.Angle(transform.rotation, targetRot);
        if (angle < 5f)
        {
            float distToPlayer = Vector3.Distance(transform.position, player.position);
            if (distToPlayer < 5f)
            {
                currentState = State.Wandering;
                hasWanderTarget = false;
                return;
            }

            if (CheckLineOfSight())
            {
                transform.rotation = targetRot;
                float yDiff = (player.position.y + playerCenterHeight) - shootPoint.position.y;
                float hDist = Vector3.Distance(
                    new Vector3(transform.position.x, 0f, transform.position.z),
                    new Vector3(player.position.x, 0f, player.position.z));
                float pitch = Mathf.Atan2(yDiff, hDist) * Mathf.Rad2Deg;
                lockedShootDirection = Quaternion.AngleAxis(-pitch, transform.right) * transform.forward;
                raisedShootDirection = GetRaisedDirection();

                preAimTimer = preAimPause;
                aimDownTimer = aimDownDuration;
                currentState = State.AimingDown;
            }
            else
            {
                currentState = State.Wandering;
                hasWanderTarget = false;
            }
        }
    }

    private void HandleAimingDown()
    {
        agent.ResetPath();
        FaceLockedDirection();

        if (preAimTimer > 0f)
        {
            preAimTimer -= Time.deltaTime;
            targetHandRotation = GetHandRotationForDirection(raisedShootDirection);
            return;
        }

        aimDownTimer -= Time.deltaTime;
        float t = Mathf.Clamp01(1f - aimDownTimer / aimDownDuration);
        Vector3 currentAimDir = Vector3.Slerp(raisedShootDirection, lockedShootDirection, t);
        targetHandRotation = GetHandRotationForDirection(currentAimDir);

        if (aimDownTimer <= 0f)
        {
            fireTimer = 0f;
            currentState = State.Shooting;
        }
    }

    private void HandleShooting()
    {
        agent.ResetPath();
        FaceLockedDirection();

        targetHandRotation = GetHandRotationForDirection(lockedShootDirection);

        fireTimer -= Time.deltaTime;
        if (fireTimer <= 0f)
        {
            Shoot();
            currentAmmo--;
            fireTimer = 1f / fireRate;

            if (currentAmmo <= 0)
            {
                reloadTimer = reloadCooldown;
                currentState = State.Reloading;
                hasWanderTarget = false;
            }
        }
    }

    private void HandleReloading()
    {
        targetHandRotation = GetHandRotationForDirection(GetLoweredDirection());

        if (!hasWanderTarget || ReachedDestination())
            PickRandomWanderPoint();

        FaceMovement();

        reloadTimer -= Time.deltaTime;
        if (reloadTimer <= 0f)
        {
            currentAmmo = maxAmmo;
            currentState = State.Wandering;
            hasWanderTarget = false;
        }
    }

    private void FaceLockedDirection()
    {
        Vector3 flatDir = lockedShootDirection;
        flatDir.y = 0f;
        if (flatDir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(flatDir);
    }

    private void PickRandomWanderPoint()
    {
        for (int i = 0; i < 30; i++)
        {
            Vector3 randomPoint = transform.position + Random.insideUnitSphere * wanderRadius;
            randomPoint.y = transform.position.y;

            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
                hasWanderTarget = true;
                return;
            }
        }

        hasWanderTarget = false;
    }

    private bool ReachedDestination()
    {
        if (!agent.hasPath) return true;
        return agent.remainingDistance <= agent.stoppingDistance && !agent.pathPending;
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

    private void Shoot()
    {
        if (shootPoint == null || projectilePrefab == null) return;

        if (animCont != null)
            animCont.Play("SilaczShoot");

        if (shootAudioSource && shootSound)
        {
            shootAudioSource.pitch = Random.Range(pitchMin, pitchMax);
            shootAudioSource.clip = shootSound;
            shootAudioSource.Play();
        }

        Vector3 spread = Random.insideUnitSphere * Mathf.Tan(spreadAngle * Mathf.Deg2Rad);
        Vector3 shotDir = (lockedShootDirection + spread).normalized;

        GameObject proj = Instantiate(projectilePrefab, shootPoint.position, Quaternion.LookRotation(shotDir));
        Rigidbody rb = proj.GetComponent<Rigidbody>();
        if (rb != null)
            rb.linearVelocity = shotDir * projectileSpeed;

        Projectajl p = proj.GetComponent<Projectajl>();
        if (p != null)
            p.damage = Random.Range(minDamage, maxDamage);
    }
}