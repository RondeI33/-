using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class Strzelacz : MonoBehaviour, IEnemy
{
    [Header("References")]
    [SerializeField] private Transform shootPoint;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform hand;
    [SerializeField] private Transform losCheckPoint;
    [SerializeField] private Transform modelTransform;

    [Header("Detection")]
    [SerializeField] private float detectionRange = 67f;
    [SerializeField] private float attackRange = 9f;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private float losHeightOffset = 1f;

    [Header("Shooting")]
    [SerializeField] private float fireRate = 2f;
    [SerializeField] private float projectileSpeed = 20f;
    [SerializeField] private float minDamage = 6f;
    [SerializeField] private float maxDamage = 7f;

    [Header("Health")]
    [SerializeField] private float health = 166f;

    [Header("Nav Agent")]
    [SerializeField] private float agentSpeed = 4.67f;
    [SerializeField] private float agentAngularSpeed = 120f;
    [SerializeField] private float agentAcceleration = 8f;
    [SerializeField] private float agentStoppingDistance = 0.33f;
    [SerializeField] private float agentRadius = 1f;
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
    [SerializeField] private AudioClip deathSound;
    [SerializeField] private float deathVolume = 1f;
    [SerializeField] private float pitchMin = 0.9f;
    [SerializeField] private float pitchMax = 1.1f;

    [Header("Off-Mesh Link Traversal")]
    [SerializeField] private float dropDuration = 0.4f;
    [SerializeField] private float jumpDuration = 0.5f;

    [Header("Jump Up")]
    [SerializeField] private float jumpUpMaxHeight = 3f;
    [SerializeField] private float jumpUpCheckInterval = 0.5f;
    [SerializeField] private float jumpUpDuration = 0.6f;
    [SerializeField] private float jumpUpChance = 0.1f;

    [Header("Combat AI")]
    [SerializeField] private float strafeSpeedMultiplier = 0.75f;
    [SerializeField] private float repositionSprintMultiplier = 1.5f;
    [SerializeField] private float minStrafeChangeTime = 0.5f;
    [SerializeField] private float maxStrafeChangeTime = 1.5f;
    [SerializeField] private float minRepositionTime = 2f;
    [SerializeField] private float maxRepositionTime = 5f;
    [SerializeField] private float dodgeOnHitChance = 0.4f;
    [SerializeField] private float approachOffsetDistance = 4f;
    [SerializeField] private float approachOffsetChangeTime = 2f;
    [SerializeField] private float sprintChance = 0.5f;
    [SerializeField] private float flankAngleThreshold = 40f;
    [SerializeField] private float minPlayerDistance = 1.5f;

    private bool dying;
    private float deathTimer;
    private Vector3 deathStartPos;
    private Vector3 deathStartScale;
    private Material[] deathMaterials;
    private Color[] originalColors;
    private Shader unlitShader;

    private NavMeshAgent agent;
    private Transform player;
    private Doors doors;
    private float fireTimer;
    private bool active;
    private float playerCenterHeight;
    private EnemyForceApplier forceApplier;

    private float wobbleTimer;
    private Vector3 modelOriginalScale;

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
    private Vector3 scaleOverride;
    private bool applyScaleOverride;

    private bool traversingLink;
    private float jumpUpCheckTimer;
    private NavMeshPath pathCache;
    private Vector3 lastStuckCheckPos;
    private float stuckTimer;

    private enum CombatState { Approach, StrafeAttack, Reposition, Flank }
    private CombatState combatState;
    private Vector3 currentMoveTarget;
    private float strafeChangeTimer;
    private float repositionTimer;
    private float flankCheckTimer;
    private int strafeDir;
    private float approachSide;
    private float approachSideTimer;
    private bool sprintReposition;
    private float[] flankAngleBuffer = new float[32];

    private float noLosJumpCooldown;

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
        agent.autoTraverseOffMeshLink = false;
        pathCache = new NavMeshPath();
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
        lastStuckCheckPos = transform.position;
        combatState = CombatState.Approach;
        repositionTimer = Random.Range(minRepositionTime, maxRepositionTime);
        flankCheckTimer = 1f;
        strafeDir = Random.value > 0.5f ? 1 : -1;
        approachSide = Random.value > 0.5f ? 1f : -1f;
        approachSideTimer = approachOffsetChangeTime;
    }

    public void TakeDamage(float damage)
    {
        if (!hitboxesEnabled) return;
        health -= damage;
        if (damage > 0f)
        {
            TriggerWobble();
            TryDodgeOnHit();
        }
        if (health <= 0f)
            Die();
    }

    public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (!hitboxesEnabled) return;
        if (damage > 0f)
            SpawnHitParticle(hitPoint, hitNormal);
        TakeDamage(damage);
    }

    private void TryDodgeOnHit()
    {
        if (dying || rising || !active) return;
        if (combatState == CombatState.Reposition) return;
        if (repositionTimer > 0f) return;
        if (Random.value > dodgeOnHitChance) return;
        EnterReposition();
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

            float elapsed = riseTimer - stretchStartTime;
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
        if (traversingLink) return;

        if (agent.isOnOffMeshLink)
        {
            OffMeshLinkData linkData = agent.currentOffMeshLinkData;
            bool isDrop = linkData.linkType == OffMeshLinkType.LinkTypeDropDown;
            float playerHeightDiff = player.position.y - transform.position.y;
            bool playerAbove = playerHeightDiff > 1f;

            if (isDrop && playerAbove)
            {
                Vector3 landingPos;
                if (TryFindLedge(out landingPos) && IsLandingClear(landingPos))
                {
                    agent.CompleteOffMeshLink();
                    agent.ResetPath();
                    StartCoroutine(JumpUp(landingPos, 0));
                    return;
                }
            }

            StartCoroutine(TraverseOffMeshLink());
            return;
        }

        float dist = Vector3.Distance(transform.position, player.position);

        if (dist > detectionRange)
        {
            agent.ResetPath();
            combatState = CombatState.Approach;
            return;
        }

        noLosJumpCooldown -= Time.deltaTime;
        UpdateCombatAI(dist);

        jumpUpCheckTimer -= Time.deltaTime;
        if (jumpUpCheckTimer <= 0f)
        {
            jumpUpCheckTimer = jumpUpCheckInterval;
            TryJumpUp(false);
        }
    }

    private void UpdateCombatAI(float dist)
    {
        bool hasLOS = CheckLineOfSight();
        bool hasShootLOS = CheckShootLineOfSight();
        bool hasThickLOS = CheckLineOfSightThick();

        Debug.DrawLine(transform.position + Vector3.up * losHeightOffset, player.position + Vector3.up * playerCenterHeight, hasLOS ? Color.green : Color.red);
        if (losCheckPoint != null)
            Debug.DrawLine(losCheckPoint.position, player.position + Vector3.up * playerCenterHeight, hasShootLOS ? Color.blue : Color.yellow);

        AimHand();
        repositionTimer -= Time.deltaTime;

        if (dist < minPlayerDistance && hasLOS && agent.isOnNavMesh)
        {
            Vector3 awayDir = transform.position - player.position;
            awayDir.y = 0f;
            awayDir = awayDir.normalized;
            Vector3 backTarget = transform.position + awayDir * (minPlayerDistance - dist + 1f);
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(backTarget, out navHit, 3f, NavMesh.AllAreas))
                agent.SetDestination(navHit.position);
            FacePlayer();
            return;
        }

        fireTimer -= Time.deltaTime;
        if (dist <= attackRange && hasShootLOS && fireTimer <= 0f)
        {
            Shoot();
            fireTimer = 1f / fireRate;
        }

        if (dist <= attackRange * 1.5f && !hasThickLOS && noLosJumpCooldown <= 0f)
        {
            if (TryJumpUp(true))
                return;
            noLosJumpCooldown = 0.5f;
        }

        switch (combatState)
        {
            case CombatState.Approach:
                UpdateApproach(dist, hasLOS, hasShootLOS);
                break;
            case CombatState.StrafeAttack:
                UpdateStrafeAttack(dist, hasLOS, hasShootLOS);
                break;
            case CombatState.Reposition:
                UpdateRepositionState(dist, hasLOS, hasShootLOS);
                break;
            case CombatState.Flank:
                UpdateFlankState(dist, hasLOS, hasShootLOS);
                break;
        }
    }

    private void UpdateApproach(float dist, bool hasLOS, bool hasShootLOS)
    {
        if (dist <= attackRange && dist >= minPlayerDistance && hasLOS)
        {
            EnterStrafeAttack();
            return;
        }

        approachSideTimer -= Time.deltaTime;
        if (approachSideTimer <= 0f)
        {
            approachSide = Random.value > 0.5f ? 1f : -1f;
            approachSideTimer = approachOffsetChangeTime + Random.Range(-0.5f, 0.5f);
        }

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.01f) return;
        Vector3 dir = toPlayer.normalized;
        Vector3 perp = new Vector3(-dir.z, 0f, dir.x);
        float stopDist = Mathf.Max(attackRange * 0.6f, minPlayerDistance);
        Vector3 target = player.position - dir * stopDist + perp * approachSide * approachOffsetDistance;

        NavMeshHit navHit;
        if (NavMesh.SamplePosition(target, out navHit, 3f, NavMesh.AllAreas))
            agent.SetDestination(navHit.position);
        else
            agent.SetDestination(player.position);

        agent.speed = agentSpeed;
        FaceMovement();
    }

    private void UpdateStrafeAttack(float dist, bool hasLOS, bool hasShootLOS)
    {
        if (dist > attackRange * 1.3f || !hasLOS)
        {
            combatState = CombatState.Approach;
            agent.speed = agentSpeed;
            return;
        }

        FacePlayer();

        strafeChangeTimer -= Time.deltaTime;
        if (strafeChangeTimer <= 0f)
        {
            PickNewStrafeTarget();
            strafeChangeTimer = Random.Range(minStrafeChangeTime, maxStrafeChangeTime);
        }

        agent.SetDestination(currentMoveTarget);
        agent.speed = agentSpeed * strafeSpeedMultiplier;

        if (repositionTimer <= 0f)
        {
            EnterReposition();
            return;
        }

        flankCheckTimer -= Time.deltaTime;
        if (flankCheckTimer <= 0f)
        {
            flankCheckTimer = 1f;
            if (ShouldFlank())
            {
                EnterFlank();
                return;
            }
        }
    }

    private void UpdateRepositionState(float dist, bool hasLOS, bool hasShootLOS)
    {
        FacePlayer();

        if (agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance < 1.5f)
        {
            if (dist <= attackRange && dist >= minPlayerDistance && hasLOS)
                EnterStrafeAttack();
            else
                combatState = CombatState.Approach;
        }
    }

    private void UpdateFlankState(float dist, bool hasLOS, bool hasShootLOS)
    {
        FacePlayer();

        if (agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance < 2f)
        {
            if (dist <= attackRange && dist >= minPlayerDistance && hasLOS)
                EnterStrafeAttack();
            else
                combatState = CombatState.Approach;
        }
    }

    private void EnterStrafeAttack()
    {
        combatState = CombatState.StrafeAttack;
        agent.speed = agentSpeed * strafeSpeedMultiplier;
        strafeDir = Random.value > 0.5f ? 1 : -1;
        PickNewStrafeTarget();
        strafeChangeTimer = Random.Range(minStrafeChangeTime, maxStrafeChangeTime);
    }

    private void EnterReposition()
    {
        combatState = CombatState.Reposition;
        sprintReposition = Random.value < sprintChance;
        agent.speed = sprintReposition ? agentSpeed * repositionSprintMultiplier : agentSpeed;

        Vector3 toEnemy = transform.position - player.position;
        toEnemy.y = 0f;
        float currentAngle = Mathf.Atan2(toEnemy.z, toEnemy.x);
        float newAngle = currentAngle + Random.Range(1.2f, 2.5f) * (Random.value > 0.5f ? 1f : -1f);
        float dist = Random.Range(Mathf.Max(attackRange * 0.5f, minPlayerDistance), attackRange * 1.2f);

        Vector3 target = player.position + new Vector3(Mathf.Cos(newAngle), 0f, Mathf.Sin(newAngle)) * dist;

        NavMeshHit navHit;
        if (NavMesh.SamplePosition(target, out navHit, 5f, NavMesh.AllAreas))
            currentMoveTarget = navHit.position;
        else
            currentMoveTarget = transform.position + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized * 5f;

        if (agent.isOnNavMesh)
            agent.SetDestination(currentMoveTarget);
        repositionTimer = Random.Range(minRepositionTime, maxRepositionTime);
    }

    private void EnterFlank()
    {
        combatState = CombatState.Flank;
        agent.speed = agentSpeed;
        currentMoveTarget = GetFlankPosition();
        if (agent.isOnNavMesh)
            agent.SetDestination(currentMoveTarget);
    }

    private void PickNewStrafeTarget()
    {
        strafeDir = Random.value > 0.3f ? -strafeDir : strafeDir;

        Vector3 toEnemy = transform.position - player.position;
        toEnemy.y = 0f;
        float currentAngle = Mathf.Atan2(toEnemy.z, toEnemy.x);
        float strafeAngle = currentAngle + strafeDir * Random.Range(25f, 65f) * Mathf.Deg2Rad;
        float dist = Mathf.Clamp(toEnemy.magnitude, Mathf.Max(attackRange * 0.4f, minPlayerDistance), attackRange);

        Vector3 target = player.position + new Vector3(Mathf.Cos(strafeAngle), 0f, Mathf.Sin(strafeAngle)) * dist;

        NavMeshHit navHit;
        if (NavMesh.SamplePosition(target, out navHit, 3f, NavMesh.AllAreas))
            currentMoveTarget = navHit.position;
        else
            currentMoveTarget = transform.position + transform.right * strafeDir * 3f;

        if (agent.isOnNavMesh)
            agent.SetDestination(currentMoveTarget);
    }

    private bool ShouldFlank()
    {
        Strzelacz[] all = FindObjectsByType<Strzelacz>(FindObjectsSortMode.None);

        Vector3 playerPos = player.position;
        Vector3 toSelf = transform.position - playerPos;
        toSelf.y = 0f;
        if (toSelf.sqrMagnitude < 0.1f) return false;
        float myAngle = Mathf.Atan2(toSelf.z, toSelf.x) * Mathf.Rad2Deg;

        int aliveNearby = 0;
        float closestAngleDiff = float.MaxValue;

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == this || all[i].dying) continue;

            Vector3 toOther = all[i].transform.position - playerPos;
            toOther.y = 0f;
            if (toOther.sqrMagnitude < 0.1f) continue;

            aliveNearby++;
            float otherAngle = Mathf.Atan2(toOther.z, toOther.x) * Mathf.Rad2Deg;
            float diff = Mathf.Abs(Mathf.DeltaAngle(myAngle, otherAngle));
            if (diff < closestAngleDiff)
                closestAngleDiff = diff;
        }

        return aliveNearby >= 1 && closestAngleDiff < flankAngleThreshold;
    }

    private Vector3 GetFlankPosition()
    {
        Strzelacz[] all = FindObjectsByType<Strzelacz>(FindObjectsSortMode.None);
        int count = 0;

        Vector3 playerPos = player.position;

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == this || all[i].dying) continue;
            Vector3 toOther = all[i].transform.position - playerPos;
            toOther.y = 0f;
            if (toOther.sqrMagnitude < 0.1f) continue;
            if (count < flankAngleBuffer.Length)
                flankAngleBuffer[count++] = Mathf.Atan2(toOther.z, toOther.x);
        }

        if (count == 0)
        {
            PickNewStrafeTarget();
            return currentMoveTarget;
        }

        System.Array.Sort(flankAngleBuffer, 0, count);

        float biggestGap = 0f;
        float gapMid = 0f;

        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            float gap = (next == 0)
                ? (flankAngleBuffer[0] + Mathf.PI * 2f - flankAngleBuffer[i])
                : (flankAngleBuffer[next] - flankAngleBuffer[i]);
            if (gap > biggestGap)
            {
                biggestGap = gap;
                gapMid = flankAngleBuffer[i] + gap * 0.5f;
            }
        }

        float flankDist = Mathf.Clamp(Vector3.Distance(transform.position, playerPos), minPlayerDistance, attackRange);
        Vector3 target = playerPos + new Vector3(Mathf.Cos(gapMid), 0f, Mathf.Sin(gapMid)) * flankDist;

        NavMeshHit navHit;
        if (NavMesh.SamplePosition(target, out navHit, 5f, NavMesh.AllAreas))
            return navHit.position;

        PickNewStrafeTarget();
        return currentMoveTarget;
    }

    private bool IsLandingUseful(Vector3 landingPos)
    {
        NavMeshHit navHit;
        if (NavMesh.SamplePosition(landingPos, out navHit, 1.5f, NavMesh.AllAreas))
        {
            NavMeshPath testPath = new NavMeshPath();
            NavMesh.CalculatePath(navHit.position, player.position, NavMesh.AllAreas, testPath);
            if (testPath.status == NavMeshPathStatus.PathComplete)
                return true;
        }

        Vector3 dropTarget;
        if (TryFindDropToward(landingPos, out dropTarget))
            return true;

        return false;
    }

    private bool IsAlreadyAtLanding(Vector3 landingPos)
    {
        float horizontalDist = Vector3.Distance(
            new Vector3(transform.position.x, 0f, transform.position.z),
            new Vector3(landingPos.x, 0f, landingPos.z));
        float verticalDist = Mathf.Abs(landingPos.y - transform.position.y);
        return horizontalDist < agentRadius * 2f && verticalDist < 1f;
    }

    private bool TryJumpUp(bool forceJump)
    {
        if (pathCache == null || traversingLink) return false;

        if (!forceJump)
        {
            if (combatState != CombatState.Approach && combatState != CombatState.Reposition)
            {
                if (Random.value > jumpUpChance) return false;
            }
        }

        Vector3 landingPos;
        if (!TryFindLedge(out landingPos)) return false;
        if (!IsLandingClear(landingPos)) return false;
        if (!IsLandingUseful(landingPos)) return false;

        if (forceJump)
        {
            float currentDist = Vector3.Distance(transform.position, player.position);
            float landingDist = Vector3.Distance(landingPos, player.position);
            if (landingDist >= currentDist) return false;

            stuckTimer = 0f;
            StartCoroutine(JumpUp(landingPos, 0));
            return true;
        }

        agent.CalculatePath(player.position, pathCache);
        bool pathIncomplete = pathCache.status != NavMeshPathStatus.PathComplete;

        if (pathIncomplete)
        {
            stuckTimer = 0f;
            StartCoroutine(JumpUp(landingPos, 0));
            return true;
        }

        float walkLength = 0f;
        Vector3[] corners = pathCache.corners;
        for (int i = 1; i < corners.Length; i++)
            walkLength += Vector3.Distance(corners[i - 1], corners[i]);

        NavMeshPath jumpPath = new NavMeshPath();
        float jumpLength = Vector3.Distance(transform.position, landingPos);
        if (NavMesh.CalculatePath(landingPos, player.position, NavMesh.AllAreas, jumpPath))
        {
            Vector3[] jCorners = jumpPath.corners;
            for (int i = 1; i < jCorners.Length; i++)
                jumpLength += Vector3.Distance(jCorners[i - 1], jCorners[i]);
        }

        if (jumpLength < walkLength)
        {
            stuckTimer = 0f;
            StartCoroutine(JumpUp(landingPos, 0));
            return true;
        }

        float movedDist = Vector3.Distance(transform.position, lastStuckCheckPos);
        lastStuckCheckPos = transform.position;
        if (agent.hasPath && movedDist < 0.1f)
        {
            stuckTimer += jumpUpCheckInterval;
            if (stuckTimer > 1.5f)
            {
                stuckTimer = 0f;
                StartCoroutine(JumpUp(landingPos, 0));
                return true;
            }
        }
        else
        {
            stuckTimer = 0f;
        }

        return false;
    }

    private bool TryFindLedge(out Vector3 landingPos)
    {
        landingPos = Vector3.zero;
        RaycastHit rayHit;
        float enemyY = transform.position.y;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        Vector3 dirToPlayer = toPlayer.sqrMagnitude > 0.001f ? toPlayer.normalized : transform.forward;

        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, dirToPlayer, out rayHit, jumpUpMaxHeight * 2f, obstacleLayer))
        {
            float wallAngle = Vector3.Angle(rayHit.normal, Vector3.up);
            if (wallAngle < 50f) return false;

            Vector3 wallPoint = rayHit.point;
            float wallDist = rayHit.distance;

            if (wallDist > agentRadius * 5f) return false;

            Vector3 topScan = wallPoint + Vector3.up * (jumpUpMaxHeight + 1f);
            if (Physics.Raycast(topScan, Vector3.down, out rayHit, jumpUpMaxHeight + 2f, obstacleLayer))
            {
                float h = rayHit.point.y - enemyY;
                if (h > 0.5f && h <= jumpUpMaxHeight)
                {
                    Vector3 candidate = rayHit.point + Vector3.up * 0.05f;
                    if (!IsAlreadyAtLanding(candidate))
                    {
                        landingPos = candidate;
                        return true;
                    }
                }
            }

            for (float y = 1f; y <= jumpUpMaxHeight; y += 0.5f)
            {
                Vector3 probe = wallPoint + Vector3.up * y + dirToPlayer * 0.5f;
                if (!Physics.Raycast(probe, dirToPlayer, 1f, obstacleLayer))
                {
                    if (Physics.Raycast(probe + Vector3.up * 0.5f, Vector3.down, out rayHit, 1.5f, obstacleLayer))
                    {
                        float h = rayHit.point.y - enemyY;
                        if (h > 0.5f && h <= jumpUpMaxHeight)
                        {
                            Vector3 candidate = rayHit.point + Vector3.up * 0.05f;
                            if (!IsAlreadyAtLanding(candidate))
                            {
                                landingPos = candidate;
                                return true;
                            }
                        }
                    }
                }
            }
        }

        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.up, out rayHit, jumpUpMaxHeight, obstacleLayer))
        {
            Vector3 surfacePoint = rayHit.point + Vector3.up * 0.2f;
            if (Physics.Raycast(surfacePoint + Vector3.up * 0.5f, Vector3.down, out rayHit, 1f, obstacleLayer))
            {
                float h = rayHit.point.y - enemyY;
                if (h > 0.5f && h <= jumpUpMaxHeight)
                {
                    Vector3 candidate = rayHit.point + Vector3.up * 0.05f;
                    if (!IsAlreadyAtLanding(candidate))
                    {
                        landingPos = candidate;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private bool TryFindDropToward(Vector3 fromPos, out Vector3 dropTarget)
    {
        dropTarget = Vector3.zero;
        if (player == null) return false;

        Vector3 toPlayer = player.position - fromPos;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.1f) return false;
        Vector3 dir = toPlayer.normalized;

        for (float fwd = 1f; fwd <= 8f; fwd += 0.5f)
        {
            Vector3 scanOrigin = fromPos + Vector3.up * 0.5f;

            RaycastHit wallHit;
            if (Physics.Raycast(scanOrigin, dir, out wallHit, fwd, obstacleLayer))
                continue;

            Vector3 scanTop = fromPos + dir * fwd + Vector3.up * 0.5f;
            RaycastHit groundHit;
            if (Physics.Raycast(scanTop, Vector3.down, out groundHit, jumpUpMaxHeight + 5f, obstacleLayer))
            {
                float surfaceAngle = Vector3.Angle(groundHit.normal, Vector3.up);
                if (surfaceAngle > 45f) continue;

                Vector3 candidate = groundHit.point + Vector3.up * 0.05f;

                NavMeshHit navHit;
                if (NavMesh.SamplePosition(candidate, out navHit, 1.5f, NavMesh.AllAreas))
                {
                    dropTarget = navHit.position;
                    return true;
                }

                dropTarget = candidate;
                return true;
            }
        }

        return false;
    }

    private bool IsLandingClear(Vector3 landingPos)
    {
        Strzelacz[] all = FindObjectsByType<Strzelacz>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == this || all[i].dying) continue;
            if (all[i].traversingLink)
            {
                float dist = Vector3.Distance(landingPos, all[i].transform.position);
                if (dist < agentRadius * 3f) return false;
            }
        }
        return true;
    }

    private IEnumerator JumpUp(Vector3 landingPos, int chainDepth)
    {
        traversingLink = true;
        if (agent.enabled)
        {
            agent.ResetPath();
            agent.enabled = false;
        }

        Vector3 startPos = transform.position;
        float heightDiff = Mathf.Abs(landingPos.y - startPos.y);
        float peakY = Mathf.Max(startPos.y, landingPos.y) + Mathf.Max(heightDiff * 0.3f, 1f);
        float elapsed = 0f;

        Vector3 jumpDir = landingPos - startPos;
        jumpDir.y = 0f;
        Quaternion targetRot = jumpDir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(jumpDir) : transform.rotation;

        while (elapsed < jumpUpDuration)
        {
            float t = elapsed / jumpUpDuration;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);

            float x = Mathf.Lerp(startPos.x, landingPos.x, t);
            float z = Mathf.Lerp(startPos.z, landingPos.z, t);
            float baseY = Mathf.Lerp(startPos.y, landingPos.y, t);
            float arc = Mathf.Sin(Mathf.PI * t) * (peakY - Mathf.Max(startPos.y, landingPos.y));

            transform.position = new Vector3(x, baseY + arc, z);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = landingPos;

        if (chainDepth < 3)
        {
            NavMeshHit navCheck;
            bool onNavMesh = NavMesh.SamplePosition(landingPos, out navCheck, 0.5f, NavMesh.AllAreas);

            if (!onNavMesh)
            {
                Vector3 dropTarget;
                if (TryFindDropToward(landingPos, out dropTarget))
                {
                    yield return StartCoroutine(JumpUp(dropTarget, chainDepth + 1));
                    yield break;
                }
            }
            else
            {
                NavMeshPath checkPath = new NavMeshPath();
                NavMesh.CalculatePath(navCheck.position, player.position, NavMesh.AllAreas, checkPath);
                if (checkPath.status != NavMeshPathStatus.PathComplete)
                {
                    Vector3 nextTarget;
                    if (TryFindDropToward(landingPos, out nextTarget))
                    {
                        yield return StartCoroutine(JumpUp(nextTarget, chainDepth + 1));
                        yield break;
                    }
                }
            }
        }

        agent.enabled = true;
        NavMeshHit warpHit;
        if (NavMesh.SamplePosition(transform.position, out warpHit, 3f, NavMesh.AllAreas))
            agent.Warp(warpHit.position);
        else
            agent.Warp(transform.position);
        traversingLink = false;
        jumpUpCheckTimer = 0f;
    }

    private IEnumerator TraverseOffMeshLink()
    {
        traversingLink = true;
        OffMeshLinkData data = agent.currentOffMeshLinkData;
        Vector3 startPos = transform.position;
        Vector3 endPos = data.endPos + Vector3.up * agent.baseOffset;
        float elapsed = 0f;

        bool isDrop = data.linkType == OffMeshLinkType.LinkTypeDropDown;
        float duration = isDrop ? dropDuration : jumpDuration;

        Vector3 linkDir = endPos - startPos;
        linkDir.y = 0f;
        Quaternion targetRot = linkDir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(linkDir) : transform.rotation;

        while (elapsed < duration)
        {
            float t = elapsed / duration;

            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);

            if (isDrop)
            {
                float horizontalT = 1f - (1f - t) * (1f - t);
                float x = Mathf.Lerp(startPos.x, endPos.x, horizontalT);
                float z = Mathf.Lerp(startPos.z, endPos.z, horizontalT);
                float y = Mathf.Lerp(startPos.y, endPos.y, t * t);
                transform.position = new Vector3(x, y, z);
            }
            else
            {
                Vector3 pos = Vector3.Lerp(startPos, endPos, t);
                float hDiff = Mathf.Abs(endPos.y - startPos.y);
                float arc = Mathf.Max(hDiff * 0.3f, 0.5f);
                pos.y += Mathf.Sin(Mathf.PI * t) * arc;
                transform.position = pos;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = endPos;
        agent.CompleteOffMeshLink();
        traversingLink = false;
        jumpUpCheckTimer = 0f;
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

    private bool CheckLineOfSightThick()
    {
        Vector3 origin = transform.position + Vector3.up * losHeightOffset;
        Vector3 target = player.position + Vector3.up * playerCenterHeight;
        Vector3 dir = target - origin;
        float dist = dir.magnitude;
        if (dist < 0.01f) return true;

        RaycastHit hit;
        return !Physics.SphereCast(origin, agentRadius * 0.5f, dir.normalized, out hit, dist, obstacleLayer);
    }

    private bool CheckShootLineOfSight()
    {
        if (losCheckPoint == null) return CheckLineOfSight();
        Vector3 origin = losCheckPoint.position;
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
            hand.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(-90f, 90f, 0f);
    }

    private void Shoot()
    {
        if (shootPoint == null || projectilePrefab == null) return;

        Vector3 target = player.position + Vector3.up * playerCenterHeight;
        Vector3 dir = (target - shootPoint.position).normalized;

        animCont.Play("StrzelaczShoot");

        if (shootAudioSource && shootSound)
        {
            shootAudioSource.pitch = Random.Range(pitchMin, pitchMax);
            shootAudioSource.clip = shootSound;
            shootAudioSource.Play();
        }

        GameObject proj = Instantiate(projectilePrefab, shootPoint.position, Quaternion.LookRotation(dir));
        Rigidbody rb = proj.GetComponent<Rigidbody>();
        if (rb != null)
            rb.linearVelocity = dir * projectileSpeed;

        Projectajl p = proj.GetComponent<Projectajl>();
        if (p != null)
            p.damage = Random.Range(minDamage, maxDamage);
    }
}