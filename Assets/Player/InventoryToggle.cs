using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class InventoryToggle : MonoBehaviour
{
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private float animationDuration = 0.5f;
    private PlayerInput playerInput;
    private bool isOpen = false;
    private bool isAnimating = false;
    private RectTransform panelRect;
    private CanvasGroup canvasGroup;
    private Vector2 originalPosition;
    private Vector2 originalPivot;
    private float slideOffset;

    public bool IsOpen => isOpen;

    void Awake()
    {
        playerInput = GetComponentInParent<PlayerInput>();
        panelRect = inventoryPanel.GetComponent<RectTransform>();

        canvasGroup = inventoryPanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = inventoryPanel.AddComponent<CanvasGroup>();

        originalPosition = panelRect.anchoredPosition;
        originalPivot = panelRect.pivot;
        slideOffset = Screen.height * 1.2f;
        inventoryPanel.SetActive(false);
    }

    void OnEnable()
    {
        playerInput.actions["Inventory"].performed += OnInventoryPressed;
    }

    void OnDisable()
    {
        playerInput.actions["Inventory"].performed -= OnInventoryPressed;
    }

    private void OnInventoryPressed(InputAction.CallbackContext ctx)
    {
        if (isAnimating) return;

        isOpen = !isOpen;

        if (isOpen)
        {
            AcidBuffRendererFeature.Instance.SetEnabled(false);
            StartCoroutine(OpenAnimation());
        }
        else
        {
            AcidBuffRendererFeature.Instance.SetEnabled(true);
            StartCoroutine(CloseAnimation());
        }
    }

    private void SetPivotWithoutMoving(Vector2 newPivot)
    {
        Vector2 delta = newPivot - panelRect.pivot;
        Vector2 offset = new Vector2(delta.x * panelRect.rect.width * panelRect.localScale.x,
                                     delta.y * panelRect.rect.height * panelRect.localScale.y);
        panelRect.pivot = newPivot;
        panelRect.anchoredPosition += offset;
    }

    private IEnumerator OpenAnimation()
    {
        isAnimating = true;
        inventoryPanel.SetActive(true);

        panelRect.pivot = originalPivot;
        panelRect.anchoredPosition = new Vector2(originalPosition.x, originalPosition.y + slideOffset);
        panelRect.localScale = new Vector3(0.85f, 1f, 1f);
        canvasGroup.alpha = 1f;

        float slideTime = animationDuration * 0.35f;
        float impactTime = animationDuration * 0.2f;
        float settleTime = animationDuration * 0.45f;

        float elapsed = 0f;
        while (elapsed < slideTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / slideTime);
            float eased = t * t * t;

            float overshoot = -slideOffset * 0.05f;
            float y = Mathf.Lerp(slideOffset, overshoot, eased);
            panelRect.anchoredPosition = new Vector2(originalPosition.x, originalPosition.y + y);
            yield return null;
        }

        SetPivotWithoutMoving(new Vector2(originalPivot.x, 0f));

        elapsed = 0f;
        while (elapsed < impactTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / impactTime);
            float eased = 1f - Mathf.Pow(1f - t, 2f);

            float scaleX = 0.85f;
            float scaleY = Mathf.Lerp(1f, 0.91f, eased);

            panelRect.localScale = new Vector3(scaleX, scaleY, 1f);
            yield return null;
        }

        SetPivotWithoutMoving(originalPivot);
        Vector2 postImpactPos = panelRect.anchoredPosition;

        elapsed = 0f;
        while (elapsed < settleTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / settleTime);

            float smooth = t * t * (3f - 2f * t);
            float spring = 1f + Mathf.Sin(smooth * Mathf.PI * 2f) * 0.03f * (1f - smooth);
            float scaleX = Mathf.Lerp(0.85f, spring, smooth);
            float scaleY = Mathf.Lerp(0.91f, spring, smooth);

            panelRect.localScale = new Vector3(scaleX, scaleY, 1f);
            panelRect.anchoredPosition = Vector2.Lerp(postImpactPos, originalPosition, smooth);
            yield return null;
        }

        panelRect.localScale = Vector3.one;
        panelRect.anchoredPosition = originalPosition;
        GamePauser.Pause(playerInput);
        isAnimating = false;
    }

    private IEnumerator CloseAnimation()
    {
        isAnimating = true;
        GamePauser.Unpause(playerInput);

        SetPivotWithoutMoving(new Vector2(originalPivot.x, 0f));

        float squishTime = animationDuration * 0.3f;
        float launchTime = animationDuration * 0.7f;

        float elapsed = 0f;
        while (elapsed < squishTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / squishTime);
            float eased = t * t;

            panelRect.localScale = new Vector3(Mathf.Lerp(1f, 1.03f, eased), Mathf.Lerp(1f, 0.92f, eased), 1f);
            yield return null;
        }

        SetPivotWithoutMoving(originalPivot);
        Vector2 launchStart = panelRect.anchoredPosition;

        elapsed = 0f;
        while (elapsed < launchTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / launchTime);
            float eased = t * t * t;

            float y = Mathf.Lerp(0f, slideOffset, eased);
            float scaleX = Mathf.Lerp(1.03f, 0.9f, Mathf.Clamp01(t * 2f));
            float scaleY = Mathf.Lerp(0.92f, 1.15f, Mathf.Clamp01(t * 2f));

            panelRect.anchoredPosition = new Vector2(launchStart.x, launchStart.y + y);
            panelRect.localScale = new Vector3(scaleX, scaleY, 1f);
            canvasGroup.alpha = 1f - eased;
            yield return null;
        }

        panelRect.localScale = Vector3.one;
        panelRect.pivot = originalPivot;
        panelRect.anchoredPosition = originalPosition;
        canvasGroup.alpha = 1f;
        inventoryPanel.SetActive(false);
        isAnimating = false;
    }
}