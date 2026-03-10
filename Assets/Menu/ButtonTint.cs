using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class ButtonTint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color hoverColor = new Color(0.7f, 0.7f, 0.7f, 1f);
    [SerializeField] private Color pressedColor = new Color(0.4f, 0.4f, 0.4f, 1f);

    private TMP_Text text;

    private void Awake()
    {
        text = GetComponentInChildren<TMP_Text>();
        text.color = normalColor;
    }

    private void OnEnable()
    {
        if (text != null)
            text.color = normalColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        text.color = hoverColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        text.color = normalColor;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        text.color = pressedColor;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        text.color = normalColor;
    }
}