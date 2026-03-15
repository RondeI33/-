using UnityEngine;

public class SpeedTrail : MonoBehaviour
{
    private LineRenderer lr;
    private float duration;
    private float timer;
    private Color colorStart;
    private Color colorEnd;

    private static Material sharedMaterial;

    public static void Spawn(Vector3 from, Vector3 to, AnimationCurve widthCurve, float widthMultiplier, float fadeTime, Color color)
    {
        GameObject go = new GameObject("SpeedTrail");
        SpeedTrail st = go.AddComponent<SpeedTrail>();
        st.Setup(from, to, widthCurve, widthMultiplier, fadeTime, color);
    }

    private void Setup(Vector3 from, Vector3 to, AnimationCurve widthCurve, float widthMultiplier, float fadeTime, Color color)
    {
        duration = fadeTime;
        timer = 0f;
        colorStart = color;
        colorEnd = new Color(color.r, color.g, color.b, 0f);

        if (sharedMaterial == null)
            sharedMaterial = new Material(Shader.Find("Sprites/Default"));

        lr = gameObject.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
        lr.material = sharedMaterial;
        lr.startColor = color;
        lr.endColor = color;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        if (widthCurve != null)
        {
            lr.widthCurve = widthCurve;
            lr.widthMultiplier = widthMultiplier;
        }
        else
        {
            lr.startWidth = 0.05f;
            lr.endWidth = 0.1f;
        }
    }

    void Update()
    {
        timer += Time.deltaTime;
        float t = timer / duration;

        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        Color c = Color.Lerp(colorStart, colorEnd, t);
        lr.startColor = c;
        lr.endColor = c;
    }
}