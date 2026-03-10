using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HitPopUp : MonoBehaviour
{
    [Header("Hit Text")]
    [SerializeField] private TMP_Text hitText;
    [SerializeField] private float displayDuration = 0.3f;
    [SerializeField] private float fadeDuration = 0.4f;
    [SerializeField] private float startScale = 1.5f;
    [SerializeField] private float startScaleRandomize = 0.3f;
    [SerializeField] private float weakpointScale = 3f;
    [SerializeField] private float minTilt = 3f;
    [SerializeField] private float maxTilt = 8f;
    [SerializeField] private float positionOffsetX = 20f;
    [SerializeField] private float positionOffsetY = 15f;
    [SerializeField] private float fallSpeed = 40f;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color weakpointColor = Color.red;
    [SerializeField]
    private string[] hitMessages = new string[]
    {
        "TRAFIŁEŚ!",
        "TRAFIENIE!",
        "TRAFIONY!",
        "CELNE!",
        "DOSTAŁ!",
        "OBERWAŁ!",
        "JEST!",
        "BAM!",
        "BACH!"
    };

    [Header("Rare Messages")]
    [SerializeField] private float rareMessageChance = 0.01f;
    [SerializeField]
    private string[] rareHitMessages = new string[]
    {
        "Czysto teoretycznie, załóżmy, załóżmy czysto teoretycznie, że jest..."
    };

    [Header("Long Message Settings")]
    [SerializeField] private int longMessageThreshold = 30;
    [SerializeField] private float longMessageScaleMultiplier = 0.4f;
    [SerializeField] private float longMessageExtraWidth = 400f;

    [Header("Crosshair")]
    [SerializeField] private Image crosshair;
    [SerializeField] private Color crosshairHitColor = Color.red;
    [SerializeField] private float crosshairHitScale = 1.3f;
    [SerializeField] private float crosshairReturnSpeed = 8f;
    private float crosshairHitTimer;
    private Vector3 textBaseScale;
    private Vector3 textBasePosition;
    private float textTimer;
    private float currentStartScale;
    private bool tiltRight;

    private Color crosshairBaseColor;
    private float crosshairScaleMultiplier = 1f;
    private float baseRectWidth;

    private void Start()
    {
        if (hitText)
        {
            textBaseScale = hitText.transform.localScale;
            textBasePosition = hitText.rectTransform.anchoredPosition;
            baseRectWidth = hitText.rectTransform.sizeDelta.x;
            SetAlpha(0f);
        }

        if (crosshair)
            crosshairBaseColor = crosshair.color;
    }

    public void ShowHit(bool weakpoint = false)
    {
        if (hitText)
        {
            string message;
            if (rareHitMessages.Length > 0 && Random.value < rareMessageChance)
                message = rareHitMessages[Random.Range(0, rareHitMessages.Length)];
            else
                message = hitMessages[Random.Range(0, hitMessages.Length)];

            bool isLong = message.Length > longMessageThreshold;

            float scaleRandom = Random.Range(-startScaleRandomize, startScaleRandomize);
            currentStartScale = (weakpoint ? weakpointScale : startScale) + scaleRandom;

            if (isLong)
                currentStartScale *= longMessageScaleMultiplier;

            Vector2 size = hitText.rectTransform.sizeDelta;
            size.x = isLong ? baseRectWidth + longMessageExtraWidth : baseRectWidth;
            hitText.rectTransform.sizeDelta = size;

            hitText.text = message;
            hitText.color = weakpoint ? weakpointColor : normalColor;
            SetAlpha(1f);
            hitText.transform.localScale = textBaseScale * currentStartScale;
            tiltRight = !tiltRight;
            float tilt = Random.Range(minTilt, maxTilt) * (tiltRight ? 1f : -1f);
            hitText.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
            hitText.rectTransform.anchoredPosition = textBasePosition + new Vector3(
                Random.Range(-positionOffsetX, positionOffsetX),
                Random.Range(-positionOffsetY, positionOffsetY),
                0f);
            textTimer = displayDuration + fadeDuration;
        }

        if (weakpoint)
        {
            string msg = hitText ? hitText.text : "";
        }

        if (crosshair)
        {
            crosshair.color = crosshairHitColor;
            crosshairScaleMultiplier = crosshairHitScale;
            crosshairHitTimer = 0f;
        }
    }

    public float GetCrosshairScaleMultiplier()
    {
        return crosshairScaleMultiplier;
    }

    private void Update()
    {
        UpdateText();
        UpdateCrosshair();
    }

    private void UpdateText()
    {
        if (!hitText || textTimer <= 0f) return;

        textTimer -= Time.deltaTime;

        if (textTimer <= 0f)
        {
            SetAlpha(0f);
            hitText.transform.localScale = textBaseScale;
            hitText.transform.localRotation = Quaternion.identity;
            hitText.rectTransform.anchoredPosition = textBasePosition;
            Vector2 size = hitText.rectTransform.sizeDelta;
            size.x = baseRectWidth;
            hitText.rectTransform.sizeDelta = size;
            textTimer = 0f;
            return;
        }

        float totalTime = displayDuration + fadeDuration;
        float elapsed = totalTime - textTimer;
        float t = Mathf.Clamp01(elapsed / totalTime);
        float fade = textTimer < fadeDuration ? textTimer / fadeDuration : 1f;

        hitText.transform.localScale = Vector3.Lerp(textBaseScale * currentStartScale, textBaseScale * 0.5f, t);
        hitText.rectTransform.anchoredPosition += Vector2.down * fallSpeed * Time.deltaTime;
        if (textTimer < fadeDuration)
            SetAlpha(fade);

    }

    private void UpdateCrosshair()
    {
        if (!crosshair) return;

        crosshairHitTimer += Time.deltaTime * crosshairReturnSpeed;
        float t = Mathf.Clamp01(crosshairHitTimer);
        crosshair.color = Color.Lerp(crosshairHitColor, crosshairBaseColor, t);
        crosshairScaleMultiplier = Mathf.Lerp(crosshairHitScale, 1f, t);
    }

    private void SetAlpha(float a)
    {
        Color c = hitText.color;
        c.a = a;
        hitText.color = c;
    }
}