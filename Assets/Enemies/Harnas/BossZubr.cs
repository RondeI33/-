using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class BossZubr : MonoBehaviour, IEnemy
{
    private enum BossState { Idle, AxeThrow, AxeSpiral, Dash, DashWithAxes, GoToCenter, Summoning, Stunned }

    [Header("References")]
    [SerializeField] private Transform harnasModel;
    [SerializeField] private Transform zubrModel;
    [SerializeField] private Transform stunRotationTarget;
    [SerializeField] private Transform throwPoint;
    [SerializeField] private Transform handTransform;
    [SerializeField] private GameObject axeVisual;
    [SerializeField] private GameObject axeProjectilePrefab;

    [Header("Health")]
    [SerializeField] private float maxHealth = 800f;

    [Header("Detection")]
    [SerializeField] private float detectionRange = 67f;
    [SerializeField] private LayerMask obstacleLayer;

    [Header("Axe Throwing")]
    [SerializeField] private float axeFireRate = 1.5f;
    [SerializeField] private int axesPerVolley = 3;
    [SerializeField] private float axeDelayBetween = 0.3f;
    [SerializeField] private float axeFlightTime = 1.2f;
    [SerializeField] private float axeRespawnTime = 0.5f;
    [SerializeField] private float predictionStrength = 0.5f;

    [Header("Axe Spiral")]
    [SerializeField] private int spiralAxeCount = 12;
    [SerializeField] private float spiralFireInterval = 0.25f;
    [SerializeField] private float spiralAxeSpeed = 12f;

    [Header("Axe Spin")]
    [SerializeField] private Vector3 axeSpinAxis = new Vector3(1f, 0f, 0f);
    [SerializeField] private float axeSpinSpeed = 720f;

    [Header("Axe Hand Animation")]
    [SerializeField] private float throwWindupDuration = 0.2f;
    [SerializeField] private Vector3 throwWindupOffset = new Vector3(0f, 0.4f, -0.6f);
    [SerializeField] private float throwSwingDuration = 0.15f;
    [SerializeField] private Vector3 throwSwingEndOffset = new Vector3(0f, 0.6f, 0.7f);
    [SerializeField] private float throwSwingArcHeight = 0.3f;
    [SerializeField] private float throwReleaseAtT = 0.5f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 25f;
    [SerializeField] private float dashChargeTime = 0.8f;
    [SerializeField] private float dashMaxDuration = 3f;
    [SerializeField] private float wallCheckRadius = 1.5f;
    [SerializeField] private float wallCheckDistance = 3.5f;
    [SerializeField] private float wallPushbackDistance = 1.5f;
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private float dashPlayerDamage = 30f;
    [SerializeField] private float dashPlayerKnockback = 35f;
    [SerializeField] private float dashPlayerCheckRadius = 2f;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private float dashStopDistance = 2f;

    [Header("Dash Charge Anger")]
    [SerializeField] private Transform dashChargeWobbleTarget;
    [SerializeField] private float chargeShakeIntensity = 0.08f;
    [SerializeField] private float chargeShakeSpeed = 40f;
    [SerializeField] private Color chargeAngerColor = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField] private float chargeAngerTintStrength = 0.4f;

    [Header("Stun")]
    [SerializeField] private float stunDuration = 4f;
    [SerializeField] private float stunWobbleSpeed = 15f;
    [SerializeField] private float stunWobbleAngle = 10f;
    [SerializeField] private Transform stunWobbleTarget;
    [SerializeField] private float stunWobbleIntensity = 0.05f;

    [Header("Stun Orbit")]
    [SerializeField] private GameObject stunOrbitPrefab;
    [SerializeField] private float stunOrbitRadius = 1.5f;
    [SerializeField] private float stunOrbitSpeed = 180f;
    [SerializeField] private float stunOrbitHeight = 2.5f;

    [Header("Immune Tint")]
    [SerializeField] private Color immuneColor = new Color(0.75f, 0.75f, 0.75f, 1f);
    [SerializeField] private float immuneTintTransitionSpeed = 2f;

    [Header("Summoning")]
    [SerializeField] private GameObject strzelaczPrefab;
    [SerializeField] private GameObject rzezbaObjectPrefab;
    [SerializeField] private GameObject remoPrefab;
    [SerializeField] private GameObject telewizjolebPrefab;
    [SerializeField] private float summonDelay = 0.5f;
    [SerializeField] private float summonCooldown = 20f;

    [Header("Nav Agent")]
    [SerializeField] private float agentSpeed = 5f;
    [SerializeField] private float agentAngularSpeed = 180f;
    [SerializeField] private float agentAcceleration = 12f;
    [SerializeField] private float agentStoppingDistance = 0.5f;
    [SerializeField] private float agentRadius = 1.5f;
    [SerializeField] private float agentHeight = 4f;

    [Header("State Timing")]
    [SerializeField] private float stateCooldown = 2f;
    [SerializeField] private float throwStateDuration = 5f;

    [Header("Hit Feedback")]
    [SerializeField] private GameObject hitParticlePrefab;
    [SerializeField] private float hitWobbleAngle = 9f;
    [SerializeField] private float hitWobbleSpeed = 2.33f;
    [SerializeField] private float hitWobbleDuration = 0.33f;

    [Header("Spawn Rise")]
    [SerializeField] private float riseDepth = 3f;
    [SerializeField] private float riseDuration = 0.5f;
    [SerializeField] private float hitboxEnableTime = 0.2f;

    [Header("Death Effect")]
    [SerializeField] private Shader deathShader;
    [SerializeField] private float deathDuration = 1.5f;
    [SerializeField] private float deathRiseHeight = 2f;
    [SerializeField] private float deathStretchY = 2.5f;
    [SerializeField] private float deathShrinkXZ = 0.05f;
    [SerializeField] private Color deathColor = new Color(1f, 0.85f, 0.3f, 1f);

    [Header("Health Bar")]
    [SerializeField] private BossHealthBar healthBar;

    [Header("Sound Effects")]
    [SerializeField] private AudioClip roarSound;
    [SerializeField] private AudioClip chargingSound;
    [SerializeField] private AudioClip stunnedSound;
    [SerializeField] private AudioClip deathSound;
    [SerializeField] private float deathVolume = 1f;
    [SerializeField] private float pitchMin = 0.9f;
    [SerializeField] private float pitchMax = 1.1f;

    private float health;
    private bool dying;
    private float deathTimer;
    private Vector3 deathStartPos;
    private Vector3 deathStartScale;
    private Material[] deathMaterials;
    private Shader unlitShader;

    private NavMeshAgent agent;
    private Transform player;
    private CharacterController playerCC;
    private Doors doors;
    private bool active;
    private float playerCenterHeight;

    private BossState currentState;
    private float stateTimer;
    private float cooldownTimer;
    private float summonCooldownTimer;
    private bool zubrImmune = true;
    private float immuneTintT = 1f;

    private float wobbleTimer;

    private Vector3 harnasOriginalScale;
    private Vector3 zubrOriginalScale;
    private Quaternion harnasOriginalRotation;
    private Quaternion zubrOriginalRotation;

    private Vector3 harnasScaleOverride;
    private Vector3 zubrScaleOverride;
    private bool applyScaleOverride;

    private bool rising;
    private float riseTimer;
    private Vector3 harnasStartLocalPos;
    private Vector3 harnasTargetLocalPos;
    private Vector3 zubrStartLocalPos;
    private Vector3 zubrTargetLocalPos;
    private bool hitboxesEnabled;
    private Collider[] harnasHitboxes;
    private Collider[] zubrHitboxes;

    private Vector3 dashDirection;
    private float dashTimer;
    private bool dashCharging;
    private float dashChargeTimer;
    private HashSet<Transform> dashHitPlayers = new HashSet<Transform>();

    private float stunTimer;
    private GameObject[] stunOrbitObjects;
    private float stunOrbitAngle;
    private Vector3 stunWobbleOriginalPos;

    private Vector3 handLocalStart;
    private bool throwing;
    private List<GameObject> spawnedEnemies = new List<GameObject>();

    private Vector3 roomCenter;
    private Vector3 roomSize;

    private Material[] zubrMaterials;
    private Color[] zubrOriginalColors;

    private Material[] harnasMaterials;
    private Color[] harnasOriginalColors;

    private Vector3 dashChargeWobbleOriginalPos;

    private AudioSource chargingAudioSource;
    private AudioSource roarAudioSource;

    public bool ZubrImmune => zubrImmune;

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
        health = maxHealth;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerCC = playerObj.GetComponent<CharacterController>();
            if (playerCC != null)
                playerCenterHeight = playerCC.center.y + 0.33f;
        }

        if (handTransform != null)
            handLocalStart = handTransform.localPosition;

        if (chargingSound != null)
        {
            chargingAudioSource = gameObject.AddComponent<AudioSource>();
            chargingAudioSource.clip = chargingSound;
            chargingAudioSource.loop = true;
            chargingAudioSource.playOnAwake = false;
            chargingAudioSource.spatialBlend = 0f;
        }

        if (roarSound != null)
        {
            roarAudioSource = gameObject.AddComponent<AudioSource>();
            roarAudioSource.clip = roarSound;
            roarAudioSource.loop = false;
            roarAudioSource.playOnAwake = false;
            roarAudioSource.spatialBlend = 0f;
        }

        if (harnasModel != null)
        {
            harnasOriginalScale = harnasModel.localScale;
            harnasOriginalRotation = stunRotationTarget != null ? stunRotationTarget.localRotation : harnasModel.localRotation;
            harnasTargetLocalPos = harnasModel.localPosition;
            harnasStartLocalPos = harnasTargetLocalPos + Vector3.down * riseDepth;
            harnasModel.localPosition = harnasStartLocalPos;
        }

        if (zubrModel != null)
        {
            zubrOriginalScale = zubrModel.localScale;
            zubrOriginalRotation = zubrModel.localRotation;
            zubrTargetLocalPos = zubrModel.localPosition;
            zubrStartLocalPos = zubrTargetLocalPos + Vector3.down * riseDepth;
            zubrModel.localPosition = zubrStartLocalPos;
        }

        if (dashChargeWobbleTarget != null)
            dashChargeWobbleOriginalPos = dashChargeWobbleTarget.localPosition;

        if (stunWobbleTarget != null)
            stunWobbleOriginalPos = stunWobbleTarget.localPosition;

        harnasHitboxes = harnasModel != null
            ? harnasModel.GetComponentsInChildren<Collider>()
            : new Collider[0];

        ZubrHitbox[] zubrHitboxComponents = GetComponentsInChildren<ZubrHitbox>(true);
        zubrHitboxes = new Collider[zubrHitboxComponents.Length];
        for (int i = 0; i < zubrHitboxComponents.Length; i++)
            zubrHitboxes[i] = zubrHitboxComponents[i].GetComponent<Collider>();

        CacheZubrMaterials();
        CacheHarnasMaterials();

        SetHarnasHitboxes(false);
        SetZubrHitboxes(false);
        ApplyImmuneTint(1f);

        rising = true;
        riseTimer = 0f;
        hitboxesEnabled = false;
        wobbleTimer = hitWobbleDuration + 1f;
        applyScaleOverride = false;
        currentState = BossState.Idle;
        summonCooldownTimer = 0f;

        CalculateRoomBounds();

        if (healthBar != null)
            healthBar.transform.SetParent(null, false);
    }

    private void CacheHarnasMaterials()
    {
        if (harnasModel == null) return;
        Renderer[] renderers = harnasModel.GetComponentsInChildren<Renderer>();
        var mats = new List<Material>();
        var colors = new List<Color>();
        foreach (Renderer r in renderers)
        {
            foreach (Material m in r.materials)
            {
                mats.Add(m);
                colors.Add(m.color);
            }
        }
        harnasMaterials = mats.ToArray();
        harnasOriginalColors = colors.ToArray();
    }

    private void CacheZubrMaterials()
    {
        if (zubrModel == null) return;
        Renderer[] renderers = zubrModel.GetComponentsInChildren<Renderer>();
        var mats = new List<Material>();
        var colors = new List<Color>();
        foreach (Renderer r in renderers)
        {
            foreach (Material m in r.materials)
            {
                mats.Add(m);
                colors.Add(m.color);
            }
        }
        zubrMaterials = mats.ToArray();
        zubrOriginalColors = colors.ToArray();
    }

    private void RestoreHarnasColors()
    {
        if (harnasMaterials == null || harnasOriginalColors == null) return;
        for (int i = 0; i < harnasMaterials.Length; i++)
        {
            if (harnasMaterials[i] != null)
                harnasMaterials[i].color = harnasOriginalColors[i];
        }
    }

    private void RestoreZubrColors()
    {
        if (zubrMaterials == null || zubrOriginalColors == null) return;
        for (int i = 0; i < zubrMaterials.Length; i++)
        {
            if (zubrMaterials[i] != null)
                zubrMaterials[i].color = zubrOriginalColors[i];
        }
    }

    private void ApplyImmuneTint(float t)
    {
        if (zubrMaterials == null || zubrOriginalColors == null) return;
        for (int i = 0; i < zubrMaterials.Length; i++)
        {
            if (zubrMaterials[i] != null)
                zubrMaterials[i].color = t >= 1f ? immuneColor : Color.Lerp(zubrOriginalColors[i], immuneColor, t);
        }
    }

    private void ApplyChargeAngerTint(float t)
    {
        if (harnasMaterials == null || harnasOriginalColors == null) return;
        for (int i = 0; i < harnasMaterials.Length; i++)
        {
            if (harnasMaterials[i] != null)
                harnasMaterials[i].color = Color.Lerp(harnasOriginalColors[i], chargeAngerColor, t);
        }
    }

    private void CalculateRoomBounds()
    {
        Doors parentDoors = GetComponentInParent<Doors>();
        if (parentDoors == null) return;

        Renderer[] renderers = parentDoors.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        roomCenter = bounds.center;
        roomSize = bounds.size;
    }

    private void LateUpdate()
    {
        if (!applyScaleOverride) return;

        if (harnasModel != null)
            harnasModel.localScale = harnasScaleOverride;
        if (zubrModel != null)
            zubrModel.localScale = zubrScaleOverride;
    }

    private void SetHarnasHitboxes(bool enabled)
    {
        if (harnasHitboxes == null) return;
        foreach (Collider col in harnasHitboxes)
            col.enabled = enabled;
    }

    private void SetZubrHitboxes(bool enabled)
    {
        if (zubrHitboxes == null) return;
        foreach (Collider col in zubrHitboxes)
        {
            if (col == null) continue;
            col.gameObject.tag = enabled ? "Weakpoint" : "IgnoreDamage";
        }
    }

    public void SetDoors(Doors room)
    {
        doors = room;
    }

    public void Activate()
    {
        active = true;
        if (healthBar != null)
            healthBar.FadeIn();
    }

    public void TakeDamage(float damage)
    {
        if (!hitboxesEnabled) return;
        health -= damage;
        if (health < 0f) health = 0f;
        if (damage > 0f)
            TriggerWobble();
        UpdateHealthBar();
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

    public void TakeZubrDamage(float damage)
    {
        if (!hitboxesEnabled) return;
        if (zubrImmune) return;
        health -= damage;
        if (health < 0f) health = 0f;
        if (damage > 0f)
            TriggerWobble();
        UpdateHealthBar();
        if (health <= 0f)
            Die();
    }

    public void TakeZubrDamage(float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (!hitboxesEnabled) return;
        if (damage > 0f)
            SpawnHitParticle(hitPoint, hitNormal);
        TakeZubrDamage(damage);
    }

    private void UpdateHealthBar()
    {
        if (healthBar != null)
            healthBar.SetHealth(health / maxHealth);
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
        wobbleTimer = 0f;
    }

    private void UpdateHitWobble()
    {
        if (wobbleTimer > hitWobbleDuration) return;

        wobbleTimer += Time.deltaTime;
        float progress = wobbleTimer / hitWobbleDuration;
        float decay = 1f - progress;
        float wave = Mathf.Sin(progress * hitWobbleSpeed * Mathf.PI);
        float scaleOffset = wave * decay * hitWobbleAngle * 0.01f;

        harnasScaleOverride = harnasOriginalScale + new Vector3(0f, 0f, scaleOffset);
        zubrScaleOverride = zubrOriginalScale + new Vector3(0f, 0f, scaleOffset);
        applyScaleOverride = true;

        if (wobbleTimer >= hitWobbleDuration)
            applyScaleOverride = false;
    }

    private void UpdateRise()
    {
        if (!rising) return;

        riseTimer += Time.deltaTime;

        if (riseTimer <= Time.deltaTime * 2f)
        {
            if (roarAudioSource != null && roarSound != null)
            {
                roarAudioSource.pitch = Random.Range(pitchMin, pitchMax);
                roarAudioSource.Play();
            }
        }

        if (!hitboxesEnabled && riseTimer >= riseDuration - hitboxEnableTime)
        {
            SetHarnasHitboxes(true);
            hitboxesEnabled = true;
        }

        float t = Mathf.Clamp01(riseTimer / riseDuration);
        float eased = t * t * (3f - 2f * t);

        if (harnasModel != null)
            harnasModel.localPosition = Vector3.Lerp(harnasStartLocalPos, harnasTargetLocalPos, eased);
        if (zubrModel != null)
            zubrModel.localPosition = Vector3.Lerp(zubrStartLocalPos, zubrTargetLocalPos, eased);

        if (t >= 1f)
        {
            if (harnasModel != null)
                harnasModel.localPosition = harnasTargetLocalPos;
            if (zubrModel != null)
                zubrModel.localPosition = zubrTargetLocalPos;
            rising = false;
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

    public void Die()
    {
        if (dying) return;

        StopChargingSound();

        bool lastEnemy = doors != null && doors.IsLastEnemy();
        if (KillSlowMotion.Instance != null)
            KillSlowMotion.Instance.Trigger(lastEnemy);

        dying = true;
        deathTimer = 0f;

        if (deathSound != null)
            AudioSource.PlayClipAtPoint(deathSound, transform.position, deathVolume);

        StopAllCoroutines();
        DestroyStunOrbitObjects();

        // Make player invincible
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            PlayerHealth ph = playerObj.GetComponent<PlayerHealth>();
            if (ph != null) ph.SetInvincible(true);
        }

        // Trigger the victory sequence on the canvas in this room
        BossVictoryTrigger victory = GetComponentInParent<BossVictoryTrigger>();
        if (victory == null)
            victory = FindFirstObjectByType<BossVictoryTrigger>();
        if (victory != null)
            victory.Trigger();

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            agent.ResetPath();
        if (agent != null)
            agent.enabled = false;

        SetHarnasHitboxes(false);
        SetZubrHitboxes(false);
        RestoreZubrColors();
        RestoreHarnasColors();

        if (stunWobbleTarget != null)
            stunWobbleTarget.localPosition = stunWobbleOriginalPos;

        if (zubrModel != null)
        {
            deathStartPos = zubrModel.localPosition;
            deathStartScale = zubrModel.localScale;
        }
        else if (harnasModel != null)
        {
            deathStartPos = harnasModel.localPosition;
            deathStartScale = harnasModel.localScale;
        }

        List<Renderer> allRenderers = new List<Renderer>();
        if (harnasModel != null)
            allRenderers.AddRange(harnasModel.GetComponentsInChildren<Renderer>());
        if (zubrModel != null)
            allRenderers.AddRange(zubrModel.GetComponentsInChildren<Renderer>());


        unlitShader = deathShader;
        var matList = new List<Material>();
        foreach (Renderer r in allRenderers)
        {
            foreach (Material m in r.materials)
            {
                matList.Add(m);
                if (unlitShader != null)
                    m.shader = unlitShader;
            }
        }

        deathMaterials = matList.ToArray();

        if (healthBar != null)
            healthBar.FadeOut();
    }

    private void UpdateDeath()
    {
        if (!dying) return;

        deathTimer += Time.deltaTime;
        float t = Mathf.Clamp01(deathTimer / deathDuration);
        float eased = t * t;

        float yScale = Mathf.Lerp(deathStartScale.y, deathStartScale.y * deathStretchY, eased);
        float xScale = Mathf.Lerp(deathStartScale.x, deathShrinkXZ, eased);
        float zScale = Mathf.Lerp(deathStartScale.z, deathShrinkXZ, eased);
        Vector3 deathScale = new Vector3(xScale, yScale, zScale);

        if (harnasModel != null)
        {
            harnasModel.localScale = deathScale;
            harnasModel.localPosition = harnasTargetLocalPos + Vector3.up * deathRiseHeight * eased;
        }
        if (zubrModel != null)
        {
            zubrModel.localScale = deathScale;
            zubrModel.localPosition = zubrTargetLocalPos + Vector3.up * deathRiseHeight * eased;
        }

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
            if (healthBar != null)
                Destroy(healthBar.gameObject);
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
        UpdateHitWobble();
        UpdateImmuneTint();

        if (rising) return;
        if (!active || player == null) return;

        if (summonCooldownTimer > 0f)
            summonCooldownTimer -= Time.deltaTime;

        float dist = Vector3.Distance(transform.position, player.position);

        if (currentState == BossState.Stunned)
        {
            UpdateStun();
            return;
        }

        if (currentState == BossState.Dash || currentState == BossState.DashWithAxes)
        {
            UpdateDash();
            return;
        }

        if (currentState != BossState.Idle && currentState != BossState.GoToCenter && currentState != BossState.Summoning)
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {
                currentState = BossState.Idle;
                cooldownTimer = stateCooldown;
            }
        }

        if (currentState == BossState.GoToCenter)
        {
            UpdateGoToCenter();
            return;
        }

        if (currentState == BossState.Summoning)
            return;

        if (currentState == BossState.Idle)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0f && dist <= detectionRange)
                PickRandomState();
            else if (dist <= detectionRange)
                FacePlayer();
            return;
        }

        if (currentState == BossState.AxeThrow || currentState == BossState.AxeSpiral)
        {
            FacePlayer();
            AimHand();
        }
    }

    private void UpdateImmuneTint()
    {
        float targetT = zubrImmune ? 1f : 0f;
        immuneTintT = Mathf.MoveTowards(immuneTintT, targetT, Time.deltaTime * immuneTintTransitionSpeed);

        if (currentState != BossState.Stunned)
            ApplyImmuneTint(immuneTintT);
    }

    private void PickRandomState()
    {
        bool canSummon = summonCooldownTimer <= 0f;

        if (canSummon)
        {
            int roll = Random.Range(0, 5);
            switch (roll)
            {
                case 0: StartAxeThrow(); break;
                case 1: StartAxeSpiral(); break;
                case 2: StartDash(false); break;
                case 3: StartDash(true); break;
                case 4: StartGoToCenter(); break;
            }
        }
        else
        {
            int roll = Random.Range(0, 4);
            switch (roll)
            {
                case 0: StartAxeThrow(); break;
                case 1: StartAxeSpiral(); break;
                case 2: StartDash(false); break;
                case 3: StartDash(true); break;
            }
        }
    }

    private void StartAxeThrow()
    {
        currentState = BossState.AxeThrow;
        stateTimer = throwStateDuration;
        StartCoroutine(AxeThrowRoutine());
    }

    private IEnumerator AxeThrowRoutine()
    {
        while (currentState == BossState.AxeThrow && !dying)
        {
            for (int i = 0; i < axesPerVolley; i++)
            {
                if (currentState != BossState.AxeThrow || dying) yield break;
                yield return StartCoroutine(ThrowSingleAxe());
                yield return new WaitForSeconds(axeDelayBetween);
            }
            yield return new WaitForSeconds(1f / axeFireRate);
        }
    }

    private IEnumerator ThrowSingleAxe()
    {
        if (handTransform == null || axeVisual == null || throwPoint == null || axeProjectilePrefab == null)
            yield break;

        throwing = true;
        axeVisual.SetActive(true);

        Vector3 windupPos = handLocalStart + throwWindupOffset;
        float elapsed = 0f;
        while (elapsed < throwWindupDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / throwWindupDuration);
            handTransform.localPosition = Vector3.Lerp(handLocalStart, windupPos, t * t);
            yield return null;
        }

        Vector3 swingEnd = handLocalStart + throwSwingEndOffset;
        bool launched = false;
        elapsed = 0f;
        while (elapsed < throwSwingDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / throwSwingDuration);
            float smooth = Mathf.Sin(t * Mathf.PI * 0.5f);
            Vector3 pos = Vector3.Lerp(handLocalStart, swingEnd, smooth);
            pos.y += Mathf.Sin(t * Mathf.PI) * throwSwingArcHeight;
            handTransform.localPosition = pos;

            if (!launched && t >= throwReleaseAtT)
            {
                LaunchAxeAtPlayer();
                axeVisual.SetActive(false);
                launched = true;
            }

            yield return null;
        }

        if (!launched)
        {
            LaunchAxeAtPlayer();
            axeVisual.SetActive(false);
        }

        handTransform.localPosition = handLocalStart;

        yield return new WaitForSeconds(axeRespawnTime);

        if (axeVisual != null)
            axeVisual.SetActive(true);
        throwing = false;
    }

    private void LaunchAxeAtPlayer()
    {
        Vector3 target = GetPredictedTarget();
        Vector3 startPos = throwPoint.position;

        GameObject proj = Instantiate(axeProjectilePrefab, startPos, Quaternion.identity);
        Rigidbody rb = proj.GetComponent<Rigidbody>();
        if (rb != null)
            rb.linearVelocity = CalculateArcVelocity(startPos, target);
        AddAxeSpin(proj);
    }

    private Vector3 GetPredictedTarget()
    {
        Vector3 directTarget = player.position + Vector3.down * 0.3f;
        if (playerCC == null) return directTarget;

        Vector3 vel = playerCC.velocity;
        vel.y = 0f;
        if (vel.sqrMagnitude < 0.1f) return directTarget;

        Vector3 predicted = player.position + vel * axeFlightTime * predictionStrength;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(predicted, out hit, 3f, NavMesh.AllAreas))
            return hit.position + Vector3.down * 0.3f;

        return directTarget;
    }

    private Vector3 CalculateArcVelocity(Vector3 from, Vector3 to)
    {
        Vector3 diff = to - from;
        Vector3 horizontal = new Vector3(diff.x, 0f, diff.z);
        float dist = horizontal.magnitude;
        float adjustedTime = Mathf.Max(0.5f, axeFlightTime * Mathf.Clamp01(dist / detectionRange));

        Vector3 velocity = horizontal / adjustedTime;
        velocity.y = (diff.y / adjustedTime) - (0.5f * Physics.gravity.y * adjustedTime);
        return velocity;
    }

    private void StartAxeSpiral()
    {
        currentState = BossState.AxeSpiral;
        stateTimer = (spiralAxeCount * spiralFireInterval) + 1f;
        StartCoroutine(AxeSpiralRoutine());
    }

    private IEnumerator AxeSpiralRoutine()
    {
        float angle = 0f;
        for (int i = 0; i < spiralAxeCount; i++)
        {
            if (currentState != BossState.AxeSpiral || dying) yield break;
            if (axeVisual != null) axeVisual.SetActive(false);

            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 spawnPos = throwPoint != null ? throwPoint.position : transform.position + Vector3.up * 2f;

            GameObject proj = Instantiate(axeProjectilePrefab, spawnPos, Quaternion.LookRotation(dir));
            Rigidbody rb = proj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 vel = dir * spiralAxeSpeed;
                vel.y = 2f;
                rb.linearVelocity = vel;
            }
            AddAxeSpin(proj);

            angle += 360f / spiralAxeCount * 1.5f;
            float halfInterval = spiralFireInterval * 0.5f;
            if (axeVisual != null) axeVisual.SetActive(true);
            yield return new WaitForSeconds(halfInterval);
            if (axeVisual != null) axeVisual.SetActive(false);
            yield return new WaitForSeconds(halfInterval);
        }
        if (axeVisual != null)
            axeVisual.SetActive(true);
    }

    private void StartDash(bool withAxes)
    {
        if (player == null) return;

        currentState = withAxes ? BossState.DashWithAxes : BossState.Dash;
        dashCharging = true;
        dashChargeTimer = dashChargeTime;
        dashHitPlayers.Clear();

        StartChargingSound();

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            agent.ResetPath();

        if (withAxes)
            StartCoroutine(DashAxeRoutine());
    }

    private IEnumerator DashAxeRoutine()
    {
        while (currentState == BossState.DashWithAxes && !dying)
        {
            if (!dashCharging)
            {
                yield return StartCoroutine(ThrowSingleAxe());
                yield return new WaitForSeconds(0.4f);
            }
            else
            {
                yield return null;
            }
        }
    }

    private void UpdateDash()
    {
        if (dashCharging)
        {
            FacePlayer();
            dashChargeTimer -= Time.deltaTime;

            float chargeProgress = 1f - Mathf.Clamp01(dashChargeTimer / dashChargeTime);

            float shakeX = Mathf.Sin(Time.time * chargeShakeSpeed) * chargeShakeIntensity * chargeProgress;
            float shakeZ = Mathf.Cos(Time.time * chargeShakeSpeed * 1.3f) * chargeShakeIntensity * 0.5f * chargeProgress;

            if (dashChargeWobbleTarget != null)
                dashChargeWobbleTarget.localPosition = dashChargeWobbleOriginalPos + new Vector3(shakeX, 0f, shakeZ);

            if (harnasModel != null)
                harnasModel.localPosition = harnasTargetLocalPos + new Vector3(shakeX, 0f, shakeZ);
            if (zubrModel != null)
                zubrModel.localPosition = zubrTargetLocalPos + new Vector3(shakeX, 0f, shakeZ);

            ApplyChargeAngerTint(chargeProgress * chargeAngerTintStrength);

            if (dashChargeTimer <= 0f)
            {
                dashCharging = false;
                dashDirection = (player.position - transform.position).normalized;
                dashDirection.y = 0f;
                dashDirection.Normalize();
                dashTimer = dashMaxDuration;

                StopChargingSound();
                RestoreHarnasColors();
                ResetModelPositions();
                if (dashChargeWobbleTarget != null)
                    dashChargeWobbleTarget.localPosition = dashChargeWobbleOriginalPos;

                if (agent != null)
                    agent.enabled = false;
            }
            return;
        }

        dashTimer -= Time.deltaTime;
        transform.position += dashDirection * dashSpeed * Time.deltaTime;

        Collider[] playerHits = Physics.OverlapSphere(transform.position + Vector3.up, dashPlayerCheckRadius, playerLayer);
        foreach (Collider col in playerHits)
        {
            PlayerHealth ph = col.GetComponentInParent<PlayerHealth>();
            if (ph != null && dashHitPlayers.Add(ph.transform))
            {
                ph.TakeDamage(dashPlayerDamage);
                ForceApplier fa = ph.GetComponent<ForceApplier>();
                if (fa != null)
                {
                    Vector3 knockDir = dashDirection;
                    knockDir.y = 0.3f;
                    knockDir.Normalize();
                    fa.AddForce(knockDir * dashPlayerKnockback, ForceMode.Impulse);
                }

                transform.position -= dashDirection * dashStopDistance;
                EndDash();
                return;
            }
        }

        if (Physics.SphereCast(transform.position + Vector3.up, wallCheckRadius, dashDirection, out RaycastHit wallHit, wallCheckDistance, wallLayer))
        {
            HitWall();
            return;
        }

        if (dashTimer <= 0f)
            EndDash();
    }

    private void HitWall()
    {
        StopAllCoroutines();
        StopChargingSound();

        transform.position -= dashDirection * wallPushbackDistance;

        if (agent != null)
        {
            agent.enabled = true;
            if (agent.isOnNavMesh)
                agent.ResetPath();
        }

        ResetModelPositions();

        if (stunnedSound != null)
            AudioSource.PlayClipAtPoint(stunnedSound, transform.position);

        currentState = BossState.Stunned;
        stunTimer = stunDuration;
        zubrImmune = false;
        stunOrbitAngle = 0f;

        SetZubrHitboxes(true);
        SpawnStunOrbitObjects();
    }

    private void EndDash()
    {
        StopAllCoroutines();
        StopChargingSound();

        if (agent != null)
        {
            agent.enabled = true;
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(transform.position, out navHit, 5f, NavMesh.AllAreas))
                transform.position = navHit.position;
        }

        ResetModelPositions();
        if (dashChargeWobbleTarget != null)
            dashChargeWobbleTarget.localPosition = dashChargeWobbleOriginalPos;
        RestoreHarnasColors();

        currentState = BossState.Idle;
        cooldownTimer = stateCooldown;
    }

    private void ResetModelPositions()
    {
        if (harnasModel != null)
            harnasModel.localPosition = harnasTargetLocalPos;
        if (zubrModel != null)
            zubrModel.localPosition = zubrTargetLocalPos;
    }

    private void SpawnStunOrbitObjects()
    {
        DestroyStunOrbitObjects();

        if (stunOrbitPrefab == null || harnasModel == null) return;

        stunOrbitObjects = new GameObject[3];
        Vector3 orbitCenter = harnasModel.position + Vector3.up * stunOrbitHeight;

        for (int i = 0; i < 3; i++)
        {
            float angle = stunOrbitAngle + (120f * i);
            float rad = angle * Mathf.Deg2Rad;
            Vector3 pos = orbitCenter + new Vector3(Mathf.Cos(rad) * stunOrbitRadius, 0f, Mathf.Sin(rad) * stunOrbitRadius);

            stunOrbitObjects[i] = Instantiate(stunOrbitPrefab, pos, Quaternion.identity);
            stunOrbitObjects[i].transform.SetParent(null);
        }
    }

    private void DestroyStunOrbitObjects()
    {
        if (stunOrbitObjects == null) return;
        for (int i = 0; i < stunOrbitObjects.Length; i++)
        {
            if (stunOrbitObjects[i] != null)
                Destroy(stunOrbitObjects[i]);
        }
        stunOrbitObjects = null;
    }

    private void UpdateStunOrbitPositions()
    {
        if (stunOrbitObjects == null || harnasModel == null) return;

        stunOrbitAngle += stunOrbitSpeed * Time.deltaTime;

        Vector3 orbitCenter = harnasModel.position + Vector3.up * stunOrbitHeight;

        for (int i = 0; i < stunOrbitObjects.Length; i++)
        {
            if (stunOrbitObjects[i] == null) continue;

            float angle = stunOrbitAngle + (120f * i);
            float rad = angle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad) * stunOrbitRadius, 0f, Mathf.Sin(rad) * stunOrbitRadius);
            stunOrbitObjects[i].transform.position = orbitCenter + offset;
        }
    }

    private void UpdateStun()
    {
        stunTimer -= Time.deltaTime;

        float wave = Mathf.Sin(Time.time * stunWobbleSpeed) * stunWobbleAngle;

        Transform harnasRotTarget = stunRotationTarget != null ? stunRotationTarget : harnasModel;
        if (harnasRotTarget != null)
            harnasRotTarget.localRotation = harnasOriginalRotation * Quaternion.Euler(0f, 0f, wave);
        if (zubrModel != null)
            zubrModel.localRotation = zubrOriginalRotation * Quaternion.Euler(0f, 0f, wave);

        if (stunWobbleTarget != null)
        {
            float wobbleY = Mathf.Sin(Time.time * stunWobbleSpeed * 1.5f) * stunWobbleIntensity;
            stunWobbleTarget.localPosition = stunWobbleOriginalPos + new Vector3(0f, wobbleY, 0f);
        }

        float stunProgress = 1f - Mathf.Clamp01(stunTimer / stunDuration);
        if (zubrMaterials != null)
        {
            for (int i = 0; i < zubrMaterials.Length; i++)
            {
                if (zubrMaterials[i] != null)
                    zubrMaterials[i].color = Color.Lerp(immuneColor, zubrOriginalColors[i], stunProgress);
            }
        }

        UpdateStunOrbitPositions();

        if (stunTimer <= 0f)
        {
            zubrImmune = true;
            SetZubrHitboxes(false);
            RestoreZubrColors();
            DestroyStunOrbitObjects();

            if (stunWobbleTarget != null)
                stunWobbleTarget.localPosition = stunWobbleOriginalPos;

            if (harnasRotTarget != null)
                harnasRotTarget.localRotation = harnasOriginalRotation;
            if (zubrModel != null)
                zubrModel.localRotation = zubrOriginalRotation;

            currentState = BossState.Idle;
            cooldownTimer = stateCooldown;
        }
    }

    private void StartGoToCenter()
    {
        currentState = BossState.GoToCenter;

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(roomCenter, out hit, 5f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
            else
                agent.SetDestination(roomCenter);
        }
    }

    private void UpdateGoToCenter()
    {
        if (agent == null) return;

        FaceMovement();

        if (!agent.pathPending && agent.remainingDistance < 1.5f)
        {
            agent.ResetPath();
            currentState = BossState.Summoning;
            StartCoroutine(SummonRoutine());
        }
    }

    private IEnumerator SummonRoutine()
    {
        yield return new WaitForSeconds(summonDelay);

        int summonType = Random.Range(0, 4);

        switch (summonType)
        {
            case 0: SpawnEnemiesAround(strzelaczPrefab, 8); break;
            case 1: SpawnEnemiesAround(rzezbaObjectPrefab, 2); break;
            case 2: SpawnEnemiesAround(remoPrefab, 4); break;
            case 3: SpawnTelewizjolebInCorners(); break;
        }

        summonCooldownTimer = summonCooldown;

        yield return new WaitForSeconds(1f);

        currentState = BossState.Idle;
        cooldownTimer = stateCooldown * 2f;
    }

    private void SpawnEnemiesAround(GameObject prefab, int count)
    {
        if (prefab == null) return;

        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count) * i;
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 4f;
            Vector3 spawnPos = transform.position + offset;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(spawnPos, out hit, 5f, NavMesh.AllAreas))
                spawnPos = hit.position;

            GameObject enemy = Instantiate(prefab, spawnPos, Quaternion.identity);
            IEnemy enemyComp = enemy.GetComponent<IEnemy>();
            if (enemyComp != null)
            {
                enemyComp.InitAgent();
                enemyComp.SetDoors(null);
                enemyComp.Activate();
            }
            spawnedEnemies.Add(enemy);
        }
    }

    private void SpawnTelewizjolebInCorners()
    {
        if (telewizjolebPrefab == null) return;

        Vector3 halfSize = roomSize * 0.5f;
        Vector3[] corners = new Vector3[]
        {
            roomCenter + new Vector3(-halfSize.x + 2f, 0f, -halfSize.z + 2f),
            roomCenter + new Vector3(halfSize.x - 2f, 0f, -halfSize.z + 2f),
            roomCenter + new Vector3(-halfSize.x + 2f, 0f, halfSize.z - 2f),
            roomCenter + new Vector3(halfSize.x - 2f, 0f, halfSize.z - 2f)
        };

        foreach (Vector3 corner in corners)
        {
            Vector3 spawnPos = corner;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(corner, out hit, 5f, NavMesh.AllAreas))
                spawnPos = hit.position;

            GameObject enemy = Instantiate(telewizjolebPrefab, spawnPos, Quaternion.identity);
            IEnemy enemyComp = enemy.GetComponent<IEnemy>();
            if (enemyComp != null)
            {
                enemyComp.InitAgent();
                enemyComp.SetDoors(null);
                enemyComp.Activate();
            }
            spawnedEnemies.Add(enemy);
        }
    }

    private void FacePlayer()
    {
        if (player == null) return;
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * agentAngularSpeed / 10f);
    }

    private void FaceMovement()
    {
        if (agent != null && agent.hasPath && agent.velocity.sqrMagnitude > 0.1f)
        {
            Vector3 dir = agent.velocity;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * agentAngularSpeed / 10f);
        }
    }

    private void AimHand()
    {
        if (handTransform == null || throwing) return;
        Vector3 target = player.position + Vector3.up * playerCenterHeight;
        Vector3 dir = target - handTransform.position;
        if (dir.sqrMagnitude > 0.001f)
            handTransform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(-90f, 90f, 0f);
    }

    private void AddAxeSpin(GameObject proj)
    {
        AxeSpinComponent spin = proj.AddComponent<AxeSpinComponent>();
        spin.axis = axeSpinAxis.normalized;
        spin.speed = axeSpinSpeed;
    }
}

public class AxeSpinComponent : MonoBehaviour
{
    public Vector3 axis = Vector3.right;
    public float speed = 720f;

    private void Update()
    {
        transform.Rotate(axis, speed * Time.deltaTime, Space.Self);
    }
}