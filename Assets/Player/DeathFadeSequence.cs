using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class DeathFadeSequence : MonoBehaviour
{
    private CanvasGroup fadePanel;
    private float cameraFollowTime;
    private float fadeDuration;
    private float holdBeforeReload;

    public void Run(CanvasGroup panel, float followTime, float fadeDur, float holdTime)
    {
        fadePanel = panel;
        cameraFollowTime = followTime;
        fadeDuration = fadeDur;
        holdBeforeReload = holdTime;

        if (fadePanel)
        {
            fadePanel.transform.SetParent(null);
            DontDestroyOnLoad(fadePanel.gameObject);
            fadePanel.gameObject.SetActive(true);
            fadePanel.alpha = 0f;
        }

        StartCoroutine(Sequence());
    }

    private IEnumerator Sequence()
    {
        
        yield return new WaitForSecondsRealtime(cameraFollowTime);

        
        if (fadePanel)
        {
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                fadePanel.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }
            fadePanel.alpha = 1f;
        }

        yield return new WaitForSecondsRealtime(holdBeforeReload);

        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (fadePanel) Destroy(fadePanel.gameObject);
        Destroy(gameObject);
    }
}