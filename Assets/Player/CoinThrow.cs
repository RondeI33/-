using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class CoinThrow : MonoBehaviour
{
    [SerializeField] private Animator coinAnimator;
    [SerializeField] private Animator handAnimator;
    [SerializeField] private AnimationClip coinClip;
    [SerializeField] private AnimationClip handClip;
    [SerializeField] private AnimationClip catchClip;
    [SerializeField] private GameObject leftObject;
    [SerializeField] private GameObject rightObject;
    [SerializeField] private GameObject throwCoin;
    [SerializeField] private GameObject reszkaCoin;
    [SerializeField] private GameObject orzelCoin;
    [SerializeField] private Transform handOffset;
    [SerializeField] private float pullOutSpeed = 3f;
    [SerializeField] private float pullOutStartZ = -1.367f;
    [SerializeField] private float handDropSpeed = 5f;
    [SerializeField] private float handReturnSpeed = 3f;
    [SerializeField] private float handAlignTargetY = 0.137f;
    [SerializeField] private float handDipTargetY = -0.3f;
    [SerializeField] private float handDipSpeed = 8f;
    [SerializeField][Range(0f, 1f)] private float weaponSwapPoint = 0.25f;
    [SerializeField] private float skipDelay = 0.3f;
    [SerializeField] private TMP_Text ammoText;
    [SerializeField] private float reloadDipAmount = -0.15f;
    [SerializeField] private float reloadDipSpeed = 6f;
    [SerializeField] private float reloadDipReturnSpeed = 4f;

    [Header("Perfect Timing Buff")]
    [SerializeField][Range(0f, 1f)] private float greenWindowStart = 0.4f;
    [SerializeField][Range(0f, 1f)] private float greenWindowEnd = 0.55f;
    [SerializeField] private Color greenFlashColor = Color.green;
    [SerializeField] private float buffDuration = 5f;
    [SerializeField] private float buffFovBoost = 15f;
    [SerializeField] private float buffFovRiseSpeed = 100f;
    [SerializeField] private float buffFovFadeSpeed = 2f;
    [SerializeField] private float buffFireRateMultiplier = 1.5f;
    [SerializeField] private float buffCooldownMultiplier = 0.6f;
    [SerializeField] private GameObject buffParticlePrefab;
    [SerializeField] private float acidRiseSpeed = 8f;
    [SerializeField] private float acidFadeSpeed = 0.5f;

    [Header("Sound Effects")]
    [SerializeField] private AudioSource coinAudioSource;
    [SerializeField] private AudioSource catchAudioSource2;
    [SerializeField] private AudioSource reloadAudioSource;
    [SerializeField] private AudioClip throwSound;
    [SerializeField] private AudioClip catchSound;
    [SerializeField] private AudioClip catchSound2;
    [SerializeField] private AudioClip catchBuffSound;
    [SerializeField] private AudioClip catchBuffSound2;
    [SerializeField] private AudioClip bonusWindowSound;
    [SerializeField] private AudioClip pullOutSound;
    [SerializeField] private AudioClip reloadSound;
    [SerializeField] private float pitchMin = 0.9f;
    [SerializeField] private float pitchMax = 1.1f;

    private PlayerInput playerInput;
    private float skipCooldown;
    private float cooldownTimer;
    private bool swapPending;
    private bool weaponSwapped;
    private Transform activePullTarget;
    private bool pulling;
    private bool animating;

    private bool handDropping;
    private bool handReturning;
    private bool catchPlayed;
    private float catchTimer;
    private float catchHalfTime;
    private bool catchTriggered;
    private float dropStartY;
    private bool handDipping;

    private bool reloading;
    private float reloadTimer;
    private float reloadClipLength;
    private int reloadSpinsCompleted;
    private Animator reloadAnimator;
    private WeaponRecoil cachedActiveRecoil;
    private float activeReloadSpeed;
    private int activeAmmoPerSpin;
    private string activeReloadAnimationName;

    private bool reloadDipping;
    private bool reloadDipReturning;
    private float reloadDipOffset;

    private Vector3 leftStartPos;
    private Quaternion leftStartRot;
    private Vector3 rightStartPos;
    private Quaternion rightStartRot;

    private bool buffActive;
    private float buffTimer;
    private float currentFovOffset;
    private float targetFovOffset;

    private Renderer throwCoinRenderer;
    private Color throwCoinOriginalColor;
    private bool greenWindowActive;
    private float fovPunch;
    private float currentAcidIntensity;
    private float targetAcidIntensity;
    private bool pendingAutoReload;
    private bool buffActivatedThisThrow;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
    }

    private void Start()
    {
        leftStartPos = leftObject.transform.localPosition;
        leftStartRot = leftObject.transform.localRotation;
        rightStartPos = rightObject.transform.localPosition;
        rightStartRot = rightObject.transform.localRotation;
        leftObject.SetActive(true);
        rightObject.SetActive(false);
        throwCoin.SetActive(false);
        ShowResultCoin();
        cachedActiveRecoil = GetActiveWeaponRecoil();
        SubscribeAmmoEmpty(cachedActiveRecoil);
        UpdateAmmoText();

        Animator leftAnim = leftObject.GetComponentInChildren<Animator>();
        if (leftAnim) leftAnim.enabled = false;
        Animator rightAnim = rightObject.GetComponentInChildren<Animator>();
        if (rightAnim) rightAnim.enabled = false;

        throwCoinRenderer = throwCoin.GetComponentInChildren<Renderer>();
        if (throwCoinRenderer != null)
            throwCoinOriginalColor = throwCoinRenderer.material.color;
    }

    private void OnEnable()
    {
        playerInput.actions["Swap"].performed += HandleSwap;
        playerInput.actions["Reload"].performed += HandleReload;
    }

    private void OnDisable()
    {
        playerInput.actions["Swap"].performed -= HandleSwap;
        playerInput.actions["Reload"].performed -= HandleReload;
        UnsubscribeAmmoEmpty(cachedActiveRecoil);
    }

    private void Update()
    {
        if (skipCooldown > 0f)
            skipCooldown -= Time.deltaTime;

        if (reloading && playerInput.actions["Attack"].WasPressedThisFrame())
            StopReload();

        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;

            float longest = Mathf.Max(coinClip.length, handClip.length);
            float progress = 1f - (cooldownTimer / longest);

            if (animating && throwCoinRenderer != null)
            {
                if (progress >= greenWindowStart && progress <= greenWindowEnd)
                {
                    if (!greenWindowActive)
                    {
                        greenWindowActive = true;
                        throwCoinRenderer.material.color = greenFlashColor;
                        PlaySound(bonusWindowSound);

                        if (buffParticlePrefab != null)
                        {
                            Camera cam = Camera.main;
                            if (cam != null)
                            {
                                GameObject particle = Instantiate(buffParticlePrefab, cam.transform);
                                particle.transform.position = throwCoin.transform.position;
                                particle.transform.localRotation = Quaternion.identity;
                                particle.transform.localScale = Vector3.one;
                                Destroy(particle, 3f);
                            }
                        }
                    }
                }
                else if (greenWindowActive)
                {
                    greenWindowActive = false;
                    throwCoinRenderer.material.color = throwCoinOriginalColor;
                }
            }

            if (animating && cooldownTimer <= longest * 0.09f)
            {
                animating = false;
                greenWindowActive = false;
                if (throwCoinRenderer != null)
                    throwCoinRenderer.material.color = throwCoinOriginalColor;
                throwCoin.SetActive(false);
                ShowResultCoin();
                handDropping = true;
                catchTriggered = false;
                dropStartY = handOffset.localPosition.y;
            }
        }

        if (handDropping)
        {
            Vector3 pos = handOffset.localPosition;
            pos.y = Mathf.MoveTowards(pos.y, handAlignTargetY, handDropSpeed * Time.deltaTime);
            handOffset.localPosition = pos;

            if (!catchTriggered)
            {
                catchTriggered = true;
                HideResultCoins();
                handAnimator.speed = 2f;
                handAnimator.Play("catch", 0, 0f);
                catchPlayed = true;
                catchHalfTime = catchClip.length / 4f;
                catchTimer = catchClip.length / 2f;

                PlayCatchSound();
                buffActivatedThisThrow = false;
            }

            if (Mathf.Approximately(pos.y, handAlignTargetY))
                handDropping = false;
        }

        if (catchPlayed)
        {
            catchTimer -= Time.deltaTime;

            float catchTotal = catchClip.length / 2f;
            float elapsed = catchTotal - catchTimer;
            float catchProgress = catchTotal > 0f ? elapsed / catchTotal : 1f;

            if (!weaponSwapped && swapPending && catchProgress >= weaponSwapPoint)
            {
                if (reloading)
                    StopReload();

                weaponSwapped = true;
                bool leftActive = leftObject.activeSelf;

                WeaponRecoil current = (leftActive ? leftObject : rightObject).GetComponentInChildren<WeaponRecoil>();
                WeaponRecoil next = (leftActive ? rightObject : leftObject).GetComponentInChildren<WeaponRecoil>();
                if (buffActive && current) current.RemoveSpeedBuff();
                if (current) current.SetReloadBlocked(false);
                if (next)
                {
                    next.SetAmmo(next.GetMaxAmmo());
                    next.SetReloadBlocked(false);
                }

                UnsubscribeAmmoEmpty(cachedActiveRecoil);
                StopAllWeaponAudio();
                leftObject.SetActive(!leftActive);
                rightObject.SetActive(leftActive);
                swapPending = false;

                GameObject newActive = leftActive ? rightObject : leftObject;
                StartPullOut(newActive.transform);
                ProjectileShoot ps = newActive.GetComponentInChildren<ProjectileShoot>();
                if (ps)
                {
                    WeaponRecoil nextWr = newActive.GetComponentInChildren<WeaponRecoil>();
                    if (nextWr && nextWr.GetCurrentAmmo() > 0) ps.ShowRocketVisual();
                }
                cachedActiveRecoil = GetActiveWeaponRecoil();
                SubscribeAmmoEmpty(cachedActiveRecoil);
                if (buffActive && cachedActiveRecoil) cachedActiveRecoil.ApplySpeedBuff(buffFireRateMultiplier, buffCooldownMultiplier);
                UpdateAmmoText();
                if (cachedActiveRecoil != null && cachedActiveRecoil.GetCurrentAmmo() <= 0)
                    pendingAutoReload = true;
            }

            if (!handDipping && catchTimer <= catchHalfTime)
                handDipping = true;

            if (handDipping)
            {
                Vector3 pos = handOffset.localPosition;
                pos.y = Mathf.MoveTowards(pos.y, handDipTargetY, handDipSpeed * Time.deltaTime);
                handOffset.localPosition = pos;
            }

            if (catchTimer <= 0f)
            {
                catchPlayed = false;
                handDipping = false;
                handAnimator.speed = 1f;
                ShowResultCoin();
                handReturning = true;

                if (pendingAutoReload)
                {
                    pendingAutoReload = false;
                    StartReload();
                }
            }
        }

        if (handReturning)
        {
            Vector3 pos = handOffset.localPosition;
            pos.y = Mathf.MoveTowards(pos.y, 0f, handReturnSpeed * Time.deltaTime);
            handOffset.localPosition = pos;

            if (Mathf.Approximately(pos.y, 0f))
                handReturning = false;
        }

        if (pulling)
        {
            Vector3 pos = activePullTarget.localPosition;
            pos.y = Mathf.MoveTowards(pos.y, 0f, pullOutSpeed * Time.deltaTime);
            activePullTarget.localPosition = pos;

            if (Mathf.Approximately(pos.y, 0f))
            {
                pulling = false;
                if (pendingAutoReload)
                {
                    pendingAutoReload = false;
                    StartReload();
                }
            }
        }

        if (reloading)
        {
            reloadTimer += Time.deltaTime * activeReloadSpeed;
            int spins = Mathf.FloorToInt(reloadTimer / reloadClipLength);

            if (spins > reloadSpinsCompleted && reloadAudioSource && reloadSound)
            {
                reloadAudioSource.pitch = Random.Range(pitchMin, pitchMax);
                reloadAudioSource.clip = reloadSound;
                reloadAudioSource.Play();
            }

            if (spins > reloadSpinsCompleted)
            {
                int spinsDone = spins - reloadSpinsCompleted;
                reloadSpinsCompleted = spins;
                WeaponRecoil wr = GetActiveWeaponRecoil();
                if (wr)
                {
                    wr.AddAmmo(spinsDone * activeAmmoPerSpin);
                    UpdateAmmoText();

                    if (wr.GetCurrentAmmo() >= wr.GetMaxAmmo())
                    {
                        StopReload();
                        return;
                    }
                }
            }

            float normalizedTime = (reloadTimer % reloadClipLength) / reloadClipLength;
            reloadAnimator.Play(activeReloadAnimationName, 0, normalizedTime);
            reloadAnimator.speed = 0f;
        }

        UpdateReloadDip();
        UpdateAmmoText();
        UpdateBuff();
        UpdateFovOffset();
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null || coinAudioSource == null) return;
        coinAudioSource.pitch = Random.Range(pitchMin, pitchMax);
        coinAudioSource.PlayOneShot(clip);
    }

    private void PlayCatchSound()
    {
        if (buffActivatedThisThrow)
        {
            PlaySound(catchBuffSound);
            if (catchBuffSound2 != null && catchAudioSource2 != null)
            {
                catchAudioSource2.pitch = Random.Range(pitchMin, pitchMax);
                catchAudioSource2.PlayOneShot(catchBuffSound2);
            }
        }
        else
        {
            PlaySound(catchSound);
            if (catchSound2 != null && catchAudioSource2 != null)
            {
                catchAudioSource2.pitch = Random.Range(pitchMin, pitchMax);
                catchAudioSource2.PlayOneShot(catchSound2);
            }
        }
    }

    private void ActivateBuff()
    {
        if (buffActive)
            RemoveBuff();

        buffActive = true;
        buffTimer = buffDuration;
        targetFovOffset = buffFovBoost;
        fovPunch = 5f;
        targetAcidIntensity = 1f;

        BuffActiveWeapons(true);
    }

    private void RemoveBuff()
    {
        buffActive = false;
        targetFovOffset = 0f;
        targetAcidIntensity = 0f;

        BuffActiveWeapons(false);
    }

    private void BuffActiveWeapons(bool apply)
    {
        WeaponRecoil leftWr = GetSideActiveRecoil(leftObject);
        WeaponRecoil rightWr = GetSideActiveRecoil(rightObject);
        if (leftWr)
        {
            if (apply) leftWr.ApplySpeedBuff(buffFireRateMultiplier, buffCooldownMultiplier);
            else leftWr.RemoveSpeedBuff();
        }
        if (rightWr)
        {
            if (apply) rightWr.ApplySpeedBuff(buffFireRateMultiplier, buffCooldownMultiplier);
            else rightWr.RemoveSpeedBuff();
        }
    }

    private WeaponRecoil GetSideActiveRecoil(GameObject side)
    {
        foreach (Transform child in side.transform)
        {
            if (!child.gameObject.activeSelf) continue;
            WeaponRecoil wr = child.GetComponentInChildren<WeaponRecoil>();
            if (wr) return wr;
        }
        return null;
    }

    private void UpdateBuff()
    {
        if (!buffActive) return;

        buffTimer -= Time.deltaTime;
        if (buffTimer <= 0f)
            RemoveBuff();
    }

    private void UpdateFovOffset()
    {
        float lerpSpeed = targetFovOffset > currentFovOffset ? buffFovRiseSpeed : buffFovFadeSpeed;
        currentFovOffset = Mathf.MoveTowards(currentFovOffset, targetFovOffset, lerpSpeed * Time.deltaTime);

        if (fovPunch > 0f)
            fovPunch = Mathf.MoveTowards(fovPunch, 0f, 30f * Time.deltaTime);

        float acidSpeed = targetAcidIntensity > currentAcidIntensity ? acidRiseSpeed : acidFadeSpeed;
        currentAcidIntensity = Mathf.MoveTowards(currentAcidIntensity, targetAcidIntensity, acidSpeed * Time.deltaTime);
        Shader.SetGlobalFloat("_AcidIntensity", currentAcidIntensity);
    }

    public float GetBuffFovOffset() => currentFovOffset + fovPunch;

    private void StartReloadDip()
    {
        reloadDipping = true;
        reloadDipReturning = false;
        reloadDipOffset = 0f;
    }

    private void UpdateReloadDip()
    {
        if (reloadDipping)
        {
            reloadDipOffset = Mathf.MoveTowards(reloadDipOffset, reloadDipAmount, reloadDipSpeed * Time.deltaTime);
            ApplyReloadDipOffset();

            if (Mathf.Approximately(reloadDipOffset, reloadDipAmount))
            {
                reloadDipping = false;
                reloadDipReturning = true;
            }
        }

        if (reloadDipReturning)
        {
            reloadDipOffset = Mathf.MoveTowards(reloadDipOffset, 0f, reloadDipReturnSpeed * Time.deltaTime);
            ApplyReloadDipOffset();

            if (Mathf.Approximately(reloadDipOffset, 0f))
                reloadDipReturning = false;
        }
    }

    private void ApplyReloadDipOffset()
    {
        GameObject active = leftObject.activeSelf ? leftObject : rightObject;
        Vector3 pos = active.transform.localPosition;
        Vector3 basePos = leftObject.activeSelf ? leftStartPos : rightStartPos;
        pos.y = basePos.y + reloadDipOffset;
        active.transform.localPosition = pos;
    }

    private void SubscribeAmmoEmpty(WeaponRecoil wr)
    {
        if (wr) wr.OnAmmoEmpty += HandleAmmoEmpty;
    }

    private void UnsubscribeAmmoEmpty(WeaponRecoil wr)
    {
        if (wr) wr.OnAmmoEmpty -= HandleAmmoEmpty;
    }

    private void HandleAmmoEmpty()
    {
        if (handDropping || catchPlayed || handReturning || reloading || pulling)
        {
            pendingAutoReload = true;
            return;
        }
        StartReload();
    }

    private void StartReload()
    {
        WeaponRecoil wr = GetActiveWeaponRecoil();
        if (wr == null || wr.GetCurrentAmmo() >= wr.GetMaxAmmo()) return;
        if (wr.GetReloadClip() == null) return;

        reloading = true;
        reloadTimer = 0f;
        reloadSpinsCompleted = 0;
        activeReloadSpeed = wr.GetReloadSpeed();
        activeAmmoPerSpin = wr.GetAmmoPerSpin();
        activeReloadAnimationName = wr.GetReloadAnimationName();

        GameObject active = leftObject.activeSelf ? leftObject : rightObject;
        reloadAnimator = active.GetComponentInChildren<Animator>();
        reloadClipLength = wr.GetReloadClip().length;

        reloadAnimator.enabled = true;
        reloadAnimator.Play(activeReloadAnimationName, 0, 0f);
        reloadAnimator.speed = 0f;
        wr.SetReloadBlocked(true);

        if (reloadAudioSource && reloadSound)
        {
            reloadAudioSource.pitch = Random.Range(pitchMin, pitchMax);
            reloadAudioSource.clip = reloadSound;
            reloadAudioSource.Play();
        }
    }

    private void StopReload()
    {
        if (!reloading) return;
        if (reloadAudioSource) reloadAudioSource.Stop();
        reloading = false;
        if (reloadAnimator)
        {
            reloadAnimator.Rebind();
            reloadAnimator.enabled = false;
        }
        leftObject.transform.localPosition = leftStartPos;
        leftObject.transform.localRotation = leftStartRot;
        rightObject.transform.localPosition = rightStartPos;
        rightObject.transform.localRotation = rightStartRot;
        WeaponRecoil wr = GetActiveWeaponRecoil();
        if (wr) wr.SetReloadBlocked(false);
        GameObject active = leftObject.activeSelf ? leftObject : rightObject;
        ProjectileShoot ps = active.GetComponentInChildren<ProjectileShoot>();
        if (ps && wr && wr.GetCurrentAmmo() > 0) ps.ShowRocketVisual();
        StartReloadDip();
    }

    private WeaponRecoil GetActiveWeaponRecoil()
    {
        GameObject active = leftObject.activeSelf ? leftObject : rightObject;
        return active.GetComponentInChildren<WeaponRecoil>();
    }

    private void UpdateAmmoText()
    {
        if (!ammoText) return;
        if (cachedActiveRecoil)
            ammoText.text = $"{cachedActiveRecoil.GetCurrentAmmo()}/{cachedActiveRecoil.GetMaxAmmo()}";
    }

    private void SkipToCatch()
    {
        animating = false;
        cooldownTimer = 0f;
        greenWindowActive = false;
        if (throwCoinRenderer != null)
            throwCoinRenderer.material.color = throwCoinOriginalColor;

        throwCoin.SetActive(false);
        HideResultCoins();

        float diff = throwCoin.transform.position.y - handAnimator.transform.position.y;
        float handRadius = handAnimator.transform.localScale.y * 0.5f;
        Vector3 pos = handOffset.localPosition;
        pos.y += diff + handRadius;
        handOffset.localPosition = pos;

        handDropping = false;
        handDipping = false;
        weaponSwapped = false;
        catchTriggered = true;
        handAnimator.speed = 2f;
        handAnimator.Play("catch", 0, 0f);
        catchPlayed = true;
        catchHalfTime = catchClip.length / 4f;
        catchTimer = catchClip.length / 2f;

        PlayCatchSound();
        buffActivatedThisThrow = false;
    }

    private void ShowResultCoin()
    {
        reszkaCoin.SetActive(leftObject.activeSelf);
        orzelCoin.SetActive(rightObject.activeSelf);
    }

    private void HideResultCoins()
    {
        reszkaCoin.SetActive(false);
        orzelCoin.SetActive(false);
    }

    private void StartPullOut(Transform target)
    {
        activePullTarget = target;
        Vector3 pos = target.localPosition;
        pos.y = pullOutStartZ;
        target.localPosition = pos;
        pulling = true;
        PlaySound(pullOutSound);
    }

    private void HandleReload(InputAction.CallbackContext ctx)
    {
        if (handDropping || catchPlayed || handReturning || reloading) return;
        StartReload();
    }

    private void HandleSwap(InputAction.CallbackContext ctx)
    {
        if (animating && !handDropping && !catchPlayed && skipCooldown <= 0f)
        {
            if (greenWindowActive)
            {
                ActivateBuff();
                buffActivatedThisThrow = true;
                greenWindowActive = false;
                if (throwCoinRenderer != null)
                    throwCoinRenderer.material.color = throwCoinOriginalColor;
            }

            if (reloading)
                StopReload();
            SkipToCatch();
            return;
        }

        if (cooldownTimer > 0f || pulling || handDropping || catchPlayed || handReturning) return;

        float longest = Mathf.Max(coinClip.length, handClip.length);
        cooldownTimer = longest;
        swapPending = true;
        weaponSwapped = false;
        animating = true;
        buffActivatedThisThrow = false;

        HideResultCoins();
        throwCoin.SetActive(true);
        skipCooldown = skipDelay;

        coinAnimator.Play("throw", 0, 0f);
        handAnimator.Play("hand", 0, 0f);

        PlaySound(throwSound);
    }

    public bool IsBusy() => animating || handDropping || catchPlayed || handReturning || pulling;

    public void ForceReset()
    {
        if (reloading)
            StopReload();

        if (buffActive)
            RemoveBuff();

        currentFovOffset = 0f;
        targetFovOffset = 0f;
        currentAcidIntensity = 0f;
        targetAcidIntensity = 0f;
        Shader.SetGlobalFloat("_AcidIntensity", 0f);

        animating = false;
        cooldownTimer = 0f;
        skipCooldown = 0f;
        swapPending = false;
        weaponSwapped = false;
        pulling = false;
        handDropping = false;
        handReturning = false;
        catchPlayed = false;
        catchTriggered = false;
        handDipping = false;
        reloadDipping = false;
        reloadDipReturning = false;
        reloadDipOffset = 0f;
        greenWindowActive = false;
        buffActivatedThisThrow = false;

        if (throwCoinRenderer != null)
            throwCoinRenderer.material.color = throwCoinOriginalColor;

        throwCoin.SetActive(false);

        handOffset.localPosition = Vector3.zero;
        handAnimator.speed = 1f;
        handAnimator.Rebind();

        leftObject.transform.localPosition = leftStartPos;
        leftObject.transform.localRotation = leftStartRot;
        rightObject.transform.localPosition = rightStartPos;
        rightObject.transform.localRotation = rightStartRot;

        ShowResultCoin();
        UpdateAmmoText();
    }

    public void RefreshActiveWeapon(bool playPullOut)
    {
        UnsubscribeAmmoEmpty(cachedActiveRecoil);
        cachedActiveRecoil = GetActiveWeaponRecoil();
        SubscribeAmmoEmpty(cachedActiveRecoil);
        UpdateAmmoText();

        if (buffActive && cachedActiveRecoil)
            cachedActiveRecoil.ApplySpeedBuff(buffFireRateMultiplier, buffCooldownMultiplier);

        if (playPullOut)
        {
            GameObject active = leftObject.activeSelf ? leftObject : rightObject;
            StartPullOut(active.transform);
        }
    }
    private void StopAllWeaponAudio()
    {
        foreach (AudioSource src in leftObject.GetComponentsInChildren<AudioSource>(true))
            src.Stop();
        foreach (AudioSource src in rightObject.GetComponentsInChildren<AudioSource>(true))
            src.Stop();
    }

    public bool IsBuffActive => buffActive;
}