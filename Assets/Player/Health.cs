using UnityEngine;
using TMPro;
using System;
using Random = UnityEngine.Random;
using System.Collections;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float regenDelay = 3f;
    [SerializeField] private float regenRate = 20f;
    [SerializeField] private TextMeshProUGUI healthLabel;
    [SerializeField] private GameObject burnIndicator;
    [SerializeField] private float burnDamageAmount = 10f;
    [SerializeField] private float burnAfterDamageAmount = 4f;
    [SerializeField] private float burnDamageInterval = 0.5f;
    [SerializeField] private float burnBlinkSpeed = 4f;
    [SerializeField] private CanvasGroup hitFlashPanel;
    [SerializeField] private float hitFlashPeakAlpha = 0.4f;
    [SerializeField] private float hitFlashDuration = 0.3f;

    [Header("Overhealth")]
    [SerializeField] private TextMeshProUGUI overhealthLabel;
    [SerializeField] private float overhealthMax = 25f;
    [SerializeField] private float overhealthBuffMax = 50f;
    [SerializeField] private float overhealthBuffGraceTime = 3f;
    [SerializeField] private float overhealthDecayRate = 10f;
    [SerializeField] private float overhealthFadeDelay = 3f;
    [SerializeField] private float overhealthFadeDuration = 1f;
    [SerializeField] private float overhealthBuffDamageMultiplier = 2f;

    [Header("Sound Effects")]
    [SerializeField] private AudioSource damageAudioSource;
    [SerializeField] private AudioSource healAudioSource;
    [SerializeField] private AudioSource deathAudioSource;
    [SerializeField] private AudioClip damageSound;
    [SerializeField] private AudioClip shieldDamageSound;
    [SerializeField] private AudioClip healSound;
    [SerializeField] private AudioClip deathSound;
    [SerializeField] private float pitchMin = 0.9f;
    [SerializeField] private float pitchMax = 1.1f;
    [SerializeField] private float healSoundInterval = 0.5f;

    private CanvasGroup burnIndicatorGroup;
    private float currentHealth;
    private float lastDamageTime;
    private float burnTimer;
    private float burnDamageTimer;
    private int fireSourceCount;
    private bool invincible = false;
    private Coroutine hitFlashCoroutine;

    private float currentOverhealth;
    private float overhealthZeroTimer;
    private float overhealthLabelAlpha = 1f;
    private bool overhealthFading;
    private bool overhealthVisible;
    private float lastBuffEndTime = -999f;
    private bool wasBuffActive;
    private CoinThrow coinThrow;
    private float lastHealSoundTime;

    public event Action OnDied;
    public event Action<float, float> OnHealthChanged;

    private void Start()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        UpdateLabel();
        if (burnIndicator)
        {
            burnIndicatorGroup = burnIndicator.GetComponent<CanvasGroup>();
            burnIndicator.SetActive(false);
        }
        if (hitFlashPanel)
            hitFlashPanel.alpha = 0f;

        coinThrow = GetComponent<CoinThrow>();
        currentOverhealth = 0f;
        if (overhealthLabel)
        {
            overhealthLabel.alpha = 0f;
            overhealthLabel.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (currentHealth < maxHealth && Time.time - lastDamageTime >= regenDelay)
        {
            float before = currentHealth;
            currentHealth = Mathf.Min(currentHealth + regenRate * Time.deltaTime, maxHealth);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            UpdateLabel();

            if (before < currentHealth && Time.time - lastHealSoundTime >= healSoundInterval)
            {
                PlayOnSource(healAudioSource, healSound);
                lastHealSoundTime = Time.time;
            }
        }

        if (fireSourceCount > 0)
        {
            burnTimer += Time.deltaTime;
        }
        else if (burnTimer > 0f)
        {
            burnTimer -= Time.deltaTime;
        }

        bool burning = fireSourceCount > 0 || burnTimer > 0f;
        if (burning)
        {
            if (burnIndicator)
            {
                burnIndicator.SetActive(true);
                if (burnIndicatorGroup)
                {
                    float t = (Mathf.Sin(Time.time * burnBlinkSpeed * Mathf.PI) + 1f) * 0.5f;
                    burnIndicatorGroup.alpha = t;
                    float s = Mathf.Lerp(1f, 1.2f, t);
                    burnIndicator.transform.localScale = new Vector3(s, s, s);
                }
            }
            burnDamageTimer -= Time.deltaTime;
            if (burnDamageTimer <= 0f)
            {
                TakeDamage(fireSourceCount > 0 ? burnDamageAmount : burnAfterDamageAmount);
                burnDamageTimer = burnDamageInterval;
            }
        }
        else if (burnIndicator)
        {
            burnIndicator.SetActive(false);
        }

        UpdateOverhealth();
    }

    private void PlayOnSource(AudioSource source, AudioClip clip)
    {
        if (clip == null || source == null) return;
        source.pitch = Random.Range(pitchMin, pitchMax);
        source.clip = clip;
        source.Play();
    }

    private void UpdateOverhealth()
    {
        bool buffActive = coinThrow && coinThrow.IsBuffActive;

        if (wasBuffActive && !buffActive)
            lastBuffEndTime = Time.time;
        wasBuffActive = buffActive;

        if (currentOverhealth > 0f)
        {
            if (!buffActive && currentOverhealth > overhealthMax)
            {
                bool inGracePeriod = Time.time - lastBuffEndTime <= overhealthBuffGraceTime;
                if (!inGracePeriod)
                    currentOverhealth = Mathf.MoveTowards(currentOverhealth, overhealthMax, overhealthDecayRate * Time.deltaTime);
            }

            overhealthZeroTimer = 0f;
            overhealthFading = false;
            overhealthLabelAlpha = 1f;
            if (!overhealthVisible && overhealthLabel)
            {
                overhealthLabel.gameObject.SetActive(true);
                overhealthVisible = true;
            }
        }
        else
        {
            if (overhealthVisible)
            {
                overhealthZeroTimer += Time.deltaTime;
                if (overhealthZeroTimer >= overhealthFadeDelay)
                    overhealthFading = true;

                if (overhealthFading)
                {
                    overhealthLabelAlpha = Mathf.MoveTowards(overhealthLabelAlpha, 0f, Time.deltaTime / overhealthFadeDuration);
                    if (overhealthLabelAlpha <= 0f)
                    {
                        overhealthVisible = false;
                        if (overhealthLabel)
                            overhealthLabel.gameObject.SetActive(false);
                    }
                }
            }
        }

        if (overhealthLabel && overhealthVisible)
        {
            overhealthLabel.alpha = overhealthLabelAlpha;
            overhealthLabel.text = Mathf.CeilToInt(currentOverhealth).ToString();
        }
    }

    public void AddOverhealth(float amount)
    {
        bool buffActive = coinThrow && coinThrow.IsBuffActive;
        float currentMax = buffActive ? overhealthBuffMax : overhealthMax;
        float gainAmount = buffActive ? amount * overhealthBuffDamageMultiplier : amount;

        currentOverhealth = Mathf.Min(currentOverhealth + gainAmount, currentMax);
        overhealthZeroTimer = 0f;
        overhealthFading = false;
        overhealthLabelAlpha = 1f;

        if (!overhealthVisible && overhealthLabel)
        {
            overhealthLabel.gameObject.SetActive(true);
            overhealthVisible = true;
        }

        if (overhealthLabel)
        {
            overhealthLabel.alpha = 1f;
            overhealthLabel.text = Mathf.CeilToInt(currentOverhealth).ToString();
        }
    }

    public void EnterFire()
    {
        fireSourceCount++;
        if (fireSourceCount == 1)
            burnDamageTimer = 0f;
    }

    public void ExitFire()
    {
        fireSourceCount = Mathf.Max(fireSourceCount - 1, 0);
    }

    public void SetInvincible(bool state)
    {
        invincible = state;
    }

    public void TakeDamage(float damage)
    {
        if (invincible) return;
        if (currentHealth <= 0f) return;

        bool hitShield = false;

        if (currentOverhealth > 0f)
        {
            float absorbed = Mathf.Min(currentOverhealth, damage);
            currentOverhealth -= absorbed;
            damage -= absorbed;
            hitShield = true;

            if (overhealthLabel)
                overhealthLabel.text = Mathf.CeilToInt(currentOverhealth).ToString();
        }

        if (damage > 0f)
        {
            currentHealth = Mathf.Max(currentHealth - damage, 0f);
            lastDamageTime = Time.time;
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            UpdateLabel();
            FlashHitPanel();

            if (currentHealth <= 0f)
            {
                PlayOnSource(deathAudioSource, deathSound);
                OnDied?.Invoke();
            }
            else
            {
                PlayOnSource(damageAudioSource, damageSound);
            }
        }
        else
        {
            FlashHitPanel();
            PlayOnSource(damageAudioSource, hitShield ? shieldDamageSound : damageSound);
        }
    }

    private void FlashHitPanel()
    {
        if (!hitFlashPanel) return;
        if (hitFlashCoroutine != null)
            StopCoroutine(hitFlashCoroutine);
        hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
    }

    private IEnumerator HitFlashRoutine()
    {
        float half = hitFlashDuration * 0.5f;
        float elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            hitFlashPanel.alpha = Mathf.Lerp(0f, hitFlashPeakAlpha, elapsed / half);
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            hitFlashPanel.alpha = Mathf.Lerp(hitFlashPeakAlpha, 0f, elapsed / half);
            yield return null;
        }
        hitFlashPanel.alpha = 0f;
    }

    private void UpdateLabel()
    {
        if (healthLabel)
            healthLabel.text = Mathf.CeilToInt(currentHealth).ToString();
    }

    public float GetHealth() => currentHealth;
    public float GetMaxHealth() => maxHealth;
    public float GetRegenRate() => regenRate;
    public bool IsRegenActive() => currentHealth < maxHealth && Time.time - lastDamageTime >= regenDelay;
    public float GetOverhealth() => currentOverhealth;

    public void Heal(float amount)
    {
        if (currentHealth <= 0f) return;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        UpdateLabel();
        PlayOnSource(healAudioSource, healSound);
    }
}