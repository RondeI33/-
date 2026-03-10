using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class BossVictoryTrigger : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private CanvasGroup victoryPanelFin;
    [SerializeField] private CanvasGroup victoryPanelQuestion;

    [Header("Timing")]
    [SerializeField] private float fadeDelay = 2f;
    [SerializeField] private float fadeDuration = 1.5f;
    [SerializeField] private float pauseBetweenPanels = 1f;
    [SerializeField] private float holdBeforeReload = 1f;

    public void Trigger()
    {
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        if (victoryPanelFin != null) victoryPanelFin.gameObject.SetActive(false);
        if (victoryPanelQuestion != null) victoryPanelQuestion.gameObject.SetActive(false);

        StartCoroutine(Sequence());
    }

    private IEnumerator Sequence()
    {
        yield return new WaitForSecondsRealtime(fadeDelay);

        yield return StartCoroutine(FadeIn(victoryPanelFin));

        yield return new WaitForSecondsRealtime(pauseBetweenPanels);

        yield return StartCoroutine(FadeIn(victoryPanelQuestion));

        
        yield return new WaitForSecondsRealtime(holdBeforeReload);

       
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.LoadScene("SampleScene");
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Destroy(gameObject);
    }

    private IEnumerator FadeIn(CanvasGroup panel)
    {
        if (panel == null) yield break;
        panel.gameObject.SetActive(true);
        panel.alpha = 0f;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            panel.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }
        panel.alpha = 1f;
    }
}