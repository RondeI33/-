using UnityEngine;

public class PreviewRotator : MonoBehaviour
{
    [SerializeField] private Vector3 rotationAxis = Vector3.up;
    [SerializeField] private float rotationDuration = 13.33f;
    [SerializeField] private bool rotateClockwise = false;

    void Update()
    {
        float rotationSpeed = rotateClockwise ? 360f / rotationDuration : -360f / rotationDuration;
        transform.Rotate(rotationAxis.normalized, rotationSpeed * Time.unscaledDeltaTime);
    }
}