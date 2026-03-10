using UnityEngine;
using UnityEngine.UI;

public class ExitArrowIndicator : MonoBehaviour
{
    [SerializeField] private Image arrowImage;
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float scaleMin = 0.8f;
    [SerializeField] private float scaleMax = 1.2f;
    [SerializeField] private Color colorA = Color.yellow;
    [SerializeField] private Color colorB = Color.cyan;
    [SerializeField] private float rotationSmoothSpeed = 8f;
    [SerializeField] private float hideDistance = 3f;

    private RectTransform arrowRect;
    private Camera mainCam;
    private Transform target;
    private bool showing;
    private Doors activeDoors;
    private float currentAngle;

    private void Start()
    {
        mainCam = Camera.main;

        if (arrowImage != null)
            arrowRect = arrowImage.GetComponent<RectTransform>();

        Hide();
    }

    public void Show(Transform exitTarget, Doors source)
    {
        target = exitTarget;
        activeDoors = source;
        showing = true;
        if (arrowImage != null)
            arrowImage.gameObject.SetActive(true);
    }

    public void Hide()
    {
        showing = false;
        target = null;
        activeDoors = null;
        if (arrowImage != null)
            arrowImage.gameObject.SetActive(false);
    }

    public Doors GetActiveDoors()
    {
        return activeDoors;
    }

    private void Update()
    {
        if (!showing || target == null || arrowImage == null || mainCam == null) return;

        if (Vector3.Distance(transform.position, target.position) <= hideDistance)
        {
            Hide();
            return;
        }

        float t = Mathf.PingPong(Time.time * pulseSpeed, 1f);
        arrowImage.color = Color.Lerp(colorA, colorB, t);

        float scale = Mathf.Lerp(scaleMin, scaleMax, t);
        arrowRect.localScale = Vector3.one * scale;

        Vector3 dirToTarget = (target.position - mainCam.transform.position).normalized;
        Vector3 localDir = mainCam.transform.InverseTransformDirection(dirToTarget);
        float targetAngle = -Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;

        currentAngle = Mathf.LerpAngle(currentAngle, targetAngle, Time.deltaTime * rotationSmoothSpeed);
        arrowRect.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
    }
}