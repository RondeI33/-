using UnityEngine;
using UnityEngine.UI;

public class BossHealthBar : MonoBehaviour
{
    [SerializeField] private RectTransform fillRect;
    [SerializeField] private Image fillImage;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeSpeed = 2f;
    [SerializeField] private Color fullColor = Color.red;
    [SerializeField] private Color lowColor = new Color(0.3f, 0f, 0f, 1f);
    [SerializeField] private float damageLerpSpeed = 5f;

    private float targetFill = 1f;
    private float currentFill = 1f;
    private float targetAlpha;
    private bool fading;
    private float fullWidth;

    private void Start()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
        targetAlpha = 0f;
        currentFill = 1f;
        targetFill = 1f;

        if (fillRect != null)
            fullWidth = fillRect.rect.width;
    }

    private void Update()
    {
        if (fading && canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, fadeSpeed * Time.unscaledDeltaTime);
            if (Mathf.Approximately(canvasGroup.alpha, targetAlpha))
                fading = false;
        }

        if (Mathf.Abs(currentFill - targetFill) > 0.001f)
        {
            currentFill = Mathf.Lerp(currentFill, targetFill, damageLerpSpeed * Time.unscaledDeltaTime);

            if (fillRect != null)
            {
                Vector2 size = fillRect.sizeDelta;
                size.x = fullWidth * currentFill;
                fillRect.sizeDelta = size;
            }

            if (fillImage != null)
                fillImage.color = Color.Lerp(lowColor, fullColor, currentFill);
        }
    }

    public void SetHealth(float normalized)
    {
        targetFill = Mathf.Clamp01(normalized);
    }

    public void FadeIn()
    {
        targetAlpha = 1f;
        fading = true;
    }

    public void FadeOut()
    {
        targetAlpha = 0f;
        fading = true;
    }
}