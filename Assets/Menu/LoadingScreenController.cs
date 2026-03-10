using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
public class LoadingScreenController : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private GameObject loadingText;
    [SerializeField] private TMP_Text loadingLabel;
    [SerializeField] private float fadeDuration = 1.5f;
    [SerializeField] private float maxBlur = 0.15f;
    [SerializeField] private float maxPixelSize = 12f;
    [SerializeField] private float dotSpeed = 0.4f;
    private bool isFading = false;
    private float fadeTimer = 0f;
    private float dotTimer = 0f;
    private int dotCount = 0;
    private string baseText;
    private PlayerInput playerInput;
    private FPSCamera fpsCamera;
    void Awake()
    {
        canvasGroup.alpha = 1f;
        LoadingBlurFeature.BlurAmount = 0f;
        LoadingBlurFeature.PixelSize = 1f;
        if (loadingLabel != null)
            baseText = loadingLabel.text;
        playerInput = FindFirstObjectByType<PlayerInput>();
        if (playerInput != null)
            playerInput.actions.FindActionMap("Player").Disable();
        fpsCamera = FindFirstObjectByType<FPSCamera>();
        if (fpsCamera != null)
            fpsCamera.SetLocked(true);
    }
    public void ActivateLoadingVisuals()
    {
        LoadingBlurFeature.BlurAmount = maxBlur;
        LoadingBlurFeature.PixelSize = maxPixelSize;
    }
    [SerializeField] private float blackFadeRatio = 0.4f;
    public void StartFadeOut()
    {
        if (loadingText != null)
            loadingText.SetActive(false);
        if (playerInput != null)
        {
            playerInput.actions.FindActionMap("Player").Enable();
            playerInput.actions["Inventory"].Disable();
        }
        isFading = true;
        fadeTimer = 0f;
    }
    void Update()
    {
        if (!isFading && loadingLabel != null)
        {
            dotTimer += Time.unscaledDeltaTime;
            if (dotTimer >= dotSpeed)
            {
                dotTimer = 0f;
                dotCount = (dotCount + 1) % 4;
                loadingLabel.text = baseText + new string('.', dotCount);
            }
        }
        if (!isFading) return;
        fadeTimer += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(fadeTimer / fadeDuration);
        float smooth = t * t * (3f - 2f * t);
        float blackT = Mathf.Clamp01(fadeTimer / (fadeDuration * blackFadeRatio));
        canvasGroup.alpha = 1f - blackT;
        LoadingBlurFeature.BlurAmount = Mathf.Lerp(maxBlur, 0f, smooth);
        LoadingBlurFeature.PixelSize = Mathf.Lerp(maxPixelSize, 1f, smooth);
        if (t >= 1f)
        {
            if (fpsCamera != null)
                fpsCamera.SetLocked(false);
            if (playerInput != null)
                playerInput.actions["Inventory"].Enable();
            isFading = false;
            LoadingBlurFeature.BlurAmount = 0f;
            LoadingBlurFeature.PixelSize = 1f;
            gameObject.SetActive(false);
        }
    }
}