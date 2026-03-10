using UnityEngine;

public class DecalFade : MonoBehaviour
{
    private Material mat;
    private Color startColor;
    private float fadeDuration;
    private float spawnTime;
    private Transform followTarget;
    private Vector3 localPosition;
    private Quaternion localRotation;

    public void Init(Material material, Color color, float duration, Transform target, Vector3 localPos, Quaternion localRot)
    {
        mat = material;
        startColor = color;
        fadeDuration = duration;
        spawnTime = Time.time;
        followTarget = target;
        localPosition = localPos;
        localRotation = localRot;
    }

    private void Update()
    {
        if (mat == null) return;

        float t = (Time.time - spawnTime) / fadeDuration;
        if (t < 1f)
            mat.SetColor("_BaseColor", Color.Lerp(startColor, Color.black, t));
        else
            mat.SetColor("_BaseColor", Color.black);

        if (followTarget != null)
        {
            transform.position = followTarget.TransformPoint(localPosition);
            transform.rotation = followTarget.rotation * localRotation;
        }
    }
}