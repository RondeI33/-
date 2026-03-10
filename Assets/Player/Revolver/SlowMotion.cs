using UnityEngine;

public class SlowMotion : MonoBehaviour
{
    [SerializeField] private Zoom zoomScript;
    [SerializeField] private WeaponRecoil weaponRecoil;
    [SerializeField] private CoinThrow coinThrow;
    [SerializeField, Range(0.01f, 1f)] private float slowMotionScale = 0.33f;
    [SerializeField] private float transitionSpeed = 10f;
    [Header("External Slomo")]
    [SerializeField, Range(0.01f, 1f)] private float externalSlomoScale = 0.33f;
    [Header("Sound Effects")]
    [SerializeField] private AudioSource zoomAudioSource;
    [SerializeField] private AudioClip zoomInSound;

    private float defaultFixedDeltaTime;
    private float currentTimeScale = 1f;
    private float externalSlomoTimer;
    private bool wasZooming;

    public static SlowMotion Instance;

    private void Awake()
    {
        defaultFixedDeltaTime = Time.fixedDeltaTime;
        Instance = this;
    }

    public void RequestSlomo(float duration)
    {
        externalSlomoTimer = Mathf.Max(externalSlomoTimer, duration);
    }

    private void Update()
    {
        if (externalSlomoTimer > 0f)
            externalSlomoTimer -= Time.unscaledDeltaTime;

        bool zooming = zoomScript != null && zoomScript.enabled && zoomScript.IsZooming();
        bool isReloading = weaponRecoil != null && weaponRecoil.IsReloading();
        bool isThrowing = coinThrow != null && coinThrow.IsBusy();
        bool shouldSlow = zooming && !isReloading && !isThrowing;

        if (zooming && !wasZooming && zoomAudioSource && zoomInSound)
        {
            zoomAudioSource.clip = zoomInSound;
            zoomAudioSource.Play();
        }
        wasZooming = zooming;

        float target = externalSlomoTimer > 0f ? externalSlomoScale : shouldSlow ? slowMotionScale : 1f;
        float killTarget = KillSlowMotion.Instance != null ? KillSlowMotion.Instance.GetTargetScale() : 1f;
        target = Mathf.Min(target, killTarget);

        currentTimeScale = Mathf.MoveTowards(currentTimeScale, target, Time.unscaledDeltaTime * transitionSpeed);
        ApplyTimeScale(currentTimeScale);
    }

    private void OnDisable()
    {
        externalSlomoTimer = 0f;
        wasZooming = false;
        currentTimeScale = Time.timeScale;
    }

    private void ApplyTimeScale(float scale)
    {
        Time.timeScale = scale;
        Time.fixedDeltaTime = defaultFixedDeltaTime * scale;
    }
}