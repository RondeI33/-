using UnityEngine;

public class LaserTrailFade : MonoBehaviour
{
    private LineRenderer trail;
    private Color baseColor;
    private float duration;
    private float timer;

    public void Init(LineRenderer lr, Color color, float fadeDuration)
    {
        trail = lr;
        baseColor = color;
        duration = fadeDuration;
        timer = fadeDuration;
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        float alpha = Mathf.Clamp01(timer / duration);
        Color faded = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
        trail.startColor = faded;
        trail.endColor = faded;

        if (timer <= 0f)
            Destroy(gameObject);
    }
}