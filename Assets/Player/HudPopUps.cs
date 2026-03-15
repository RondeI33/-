using UnityEngine;
using TMPro;

public class HudPopUps : MonoBehaviour
{
    [SerializeField] private TMP_Text healthAddText;
    [SerializeField] private TMP_Text healthRemoveText;
    [SerializeField] private TMP_Text ammoAddText;
    [SerializeField] private TMP_Text ammoRemoveText;
    [SerializeField] private float displayDuration = 0.5f;
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private float scaleBoost = 1.3f;
    [SerializeField] private PlayerHealth playerHealth;

    private PopupState healthAdd;
    private PopupState healthRemove;
    private PopupState ammoAdd;
    private PopupState ammoRemove;

    private int lastDisplayedHealth;
    private int lastAmmo;
    private bool healthInitialized;

    private struct PopupState
    {
        public float timer;
        public Vector3 baseScale;
    }

    private void Start()
    {
        healthAdd.baseScale = InitText(healthAddText);
        healthRemove.baseScale = InitText(healthRemoveText);
        ammoAdd.baseScale = InitText(ammoAddText);
        ammoRemove.baseScale = InitText(ammoRemoveText);

        if (playerHealth)
            playerHealth.OnHealthChanged += HandleHealthChanged;

        WeaponReloadController.OnAnyAmmoChanged += HandleAmmoChanged;
        WeaponReloadController.OnAnyReloadStart += HandleReloadStart;

        WeaponReloadController existing = FindFirstObjectByType<WeaponReloadController>();
        if (existing != null)
            lastAmmo = existing.GetCurrentAmmo();
    }

    private void OnDestroy()
    {
        if (playerHealth)
            playerHealth.OnHealthChanged -= HandleHealthChanged;

        WeaponReloadController.OnAnyAmmoChanged -= HandleAmmoChanged;
        WeaponReloadController.OnAnyReloadStart -= HandleReloadStart;
    }

    private Vector3 InitText(TMP_Text text)
    {
        if (!text) return Vector3.one;
        SetAlpha(text, 0f);
        RectTransform rt = text.rectTransform;
        rt.pivot = new Vector2(0.5f, 0.5f);
        return text.transform.localScale;
    }

    private void Update()
    {
        UpdateFade(healthAddText, ref healthAdd);
        UpdateFade(healthRemoveText, ref healthRemove);
        UpdateFade(ammoAddText, ref ammoAdd);
        UpdateFade(ammoRemoveText, ref ammoRemove);
    }

    private void HandleHealthChanged(float current, float max)
    {
        if (!healthInitialized)
        {
            healthInitialized = true;
            lastDisplayedHealth = Mathf.CeilToInt(current);
            return;
        }

        int currentInt = Mathf.CeilToInt(current);
        int delta = currentInt - lastDisplayedHealth;

        if (delta == 0) return;

        lastDisplayedHealth = currentInt;

        if (delta > 0)
            ShowPopup(healthAddText, ref healthAdd, $"+{Mathf.Abs(delta)}");
        else
            ShowPopup(healthRemoveText, ref healthRemove, $"-{Mathf.Abs(delta)}");
    }

    private void HandleReloadStart(int ammoBeforeReload)
    {
        lastAmmo = ammoBeforeReload;
    }

    private void HandleAmmoChanged(int current, int max)
    {
        int delta = current - lastAmmo;
        lastAmmo = current;

        if (delta == 0) return;

        if (delta > 0)
            ShowPopup(ammoAddText, ref ammoAdd, $"+{delta}");
        else
            ShowPopup(ammoRemoveText, ref ammoRemove, $"{delta}");
    }

    private void ShowPopup(TMP_Text text, ref PopupState state, string value)
    {
        if (!text) return;
        text.text = value;
        SetAlpha(text, 1f);
        text.transform.localScale = state.baseScale * scaleBoost;
        state.timer = displayDuration + fadeDuration;
    }

    private void UpdateFade(TMP_Text text, ref PopupState state)
    {
        if (!text || state.timer <= 0f) return;

        state.timer -= Time.deltaTime;
        float totalTime = displayDuration + fadeDuration;

        if (state.timer <= 0f)
        {
            SetAlpha(text, 0f);
            text.transform.localScale = state.baseScale;
            state.timer = 0f;
        }
        else if (state.timer < fadeDuration)
        {
            float t = state.timer / fadeDuration;
            SetAlpha(text, t);
            text.transform.localScale = state.baseScale;
        }
        else
        {
            float elapsed = totalTime - state.timer;
            float t = Mathf.Clamp01(elapsed / displayDuration);
            text.transform.localScale = Vector3.Lerp(state.baseScale * scaleBoost, state.baseScale, t);
        }
    }

    private void SetAlpha(TMP_Text text, float a)
    {
        if (!text) return;
        Color c = text.color;
        c.a = a;
        text.color = c;
    }
}