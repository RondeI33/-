using UnityEngine;
public class RPGFog : MonoBehaviour
{
    [SerializeField] private Color fogColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    [SerializeField] private FogMode fogMode = FogMode.ExponentialSquared;
    [SerializeField] private float fogDensity = 0.05f;
    [SerializeField] private float fogStart = 0f;
    [SerializeField] private float fogEnd = 50f;

    [Header("Rocket Clear")]
    [SerializeField] private float clearDuration = 4f;
    [SerializeField] private float fadeDuration = 3f;

    private bool wasFogEnabled;
    private Color previousColor;
    private FogMode previousMode;
    private float previousDensity;
    private float previousStart;
    private float previousEnd;

    private float clearTimer;
    private float fadeTimer;
    private bool cleared;
    private bool fading;

    public static RPGFog Instance;

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        wasFogEnabled = RenderSettings.fog;
        previousColor = RenderSettings.fogColor;
        previousMode = RenderSettings.fogMode;
        previousDensity = RenderSettings.fogDensity;
        previousStart = RenderSettings.fogStartDistance;
        previousEnd = RenderSettings.fogEndDistance;
        RenderSettings.fog = true;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogMode = fogMode;
        RenderSettings.fogDensity = fogDensity;
        RenderSettings.fogStartDistance = fogStart;
        RenderSettings.fogEndDistance = fogEnd;
    }

    private void OnDisable()
    {
        RenderSettings.fog = wasFogEnabled;
        RenderSettings.fogColor = previousColor;
        RenderSettings.fogMode = previousMode;
        RenderSettings.fogDensity = previousDensity;
        RenderSettings.fogStartDistance = previousStart;
        RenderSettings.fogEndDistance = previousEnd;
    }

    private void Update()
    {
        if (cleared)
        {
            clearTimer -= Time.deltaTime;
            if (clearTimer <= 0f)
            {
                cleared = false;
                fading = true;
                fadeTimer = 0f;
                RenderSettings.fog = true;
                RenderSettings.fogMode = fogMode;
                RenderSettings.fogStartDistance = fogStart;
                RenderSettings.fogEndDistance = fogEnd;
                RenderSettings.fogColor = fogColor;
            }
        }

        if (fading)
        {
            fadeTimer += Time.deltaTime;
            float t = Mathf.Clamp01(fadeTimer / fadeDuration);
            RenderSettings.fogDensity = Mathf.Lerp(0f, fogDensity, t);
            if (t >= 1f)
                fading = false;
        }
    }

    public void ClearFogTemporarily()
    {
        RenderSettings.fog = false;
        RenderSettings.fogDensity = 0f;
        clearTimer = clearDuration;
        cleared = true;
        fading = false;
    }
}