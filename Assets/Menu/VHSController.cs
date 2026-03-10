using UnityEngine;
using System.Collections;

public class VHSController : MonoBehaviour
{
    [SerializeField] private Material vhsMaterial;
    [SerializeField] private float fadeOutDuration = 2f;
    [SerializeField] private float fadeOutDelay = 0f;

    private static readonly int IntensityID = Shader.PropertyToID("_Intensity");

    private void OnEnable()
    {
        if (vhsMaterial)
            vhsMaterial.SetFloat(IntensityID, 1f);
    }

    public void FadeOut()
    {
        StartCoroutine(FadeRoutine());
    }

    private IEnumerator FadeRoutine()
    {
        if (!vhsMaterial) yield break;

        yield return new WaitForSecondsRealtime(fadeOutDelay);

        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            vhsMaterial.SetFloat(IntensityID, 1f - Mathf.Clamp01(elapsed / fadeOutDuration));
            yield return null;
        }
        vhsMaterial.SetFloat(IntensityID, 0f);
    }

    private void OnDisable()
    {
        if (vhsMaterial)
            vhsMaterial.SetFloat(IntensityID, 0f);
    }
    private static readonly int TimeID = Shader.PropertyToID("_UnscaledTime");

    private void Update()
    {
        if (vhsMaterial)
            vhsMaterial.SetFloat(TimeID, Time.unscaledTime);
    }
}