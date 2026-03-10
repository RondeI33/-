using UnityEngine;

public class TitleWobble : MonoBehaviour
{
    [Header("Rotation")]
    public float rotationAmplitude = 4f;
    public float rotationSpeed = 0.6f;

    [Header("Scale Pulse")]
    public float scaleAmplitude = 0.05f;
    public float scaleSpeed = 0.9f;

    [Header("Phase Offsets")]
    public float rotationPhaseOffset = 0f;
    public float scalePhaseOffset = 1.2f;

    private Vector3 _baseScale;

    void Awake()
    {
        _baseScale = transform.localScale;
    }

    void Update()
    {
        float t = Time.unscaledTime;

        float angle = rotationAmplitude * Mathf.Sin(t * rotationSpeed * Mathf.PI * 2f + rotationPhaseOffset);
        transform.localRotation = Quaternion.Euler(0f, 0f, angle);

        float scaleFactor = 1f + scaleAmplitude * Mathf.Sin(t * scaleSpeed * Mathf.PI * 2f + scalePhaseOffset);
        transform.localScale = _baseScale * scaleFactor;
    }
}