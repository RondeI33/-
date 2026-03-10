using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class DeathHandler : MonoBehaviour
{
    [SerializeField] private GameObject deathPrefab;
    [SerializeField] private CanvasGroup fadePanel;
    [SerializeField] private float cameraFollowTime = 2f;
    [SerializeField] private float fadeDuration = 1.5f;
    [SerializeField] private float holdBeforeReload = 1f;

    private PlayerHealth playerHealth;

    private void OnEnable()
    {
        playerHealth = GetComponent<PlayerHealth>();
        playerHealth.OnDied += HandleDeath;
    }

    private void OnDisable()
    {
        playerHealth.OnDied -= HandleDeath;
    }

    private void HandleDeath()
    {
        Camera playerCam = Camera.main;

       
        GameObject deathObj = Instantiate(deathPrefab, transform.position, transform.rotation);

        
        Camera deathCam = deathObj.GetComponentInChildren<Camera>();
        if (deathCam && playerCam)
        {
            deathCam.transform.position = playerCam.transform.position;
            deathCam.transform.rotation = playerCam.transform.rotation;
            deathCam.fieldOfView = playerCam.fieldOfView;
        }

       
        if (playerCam) playerCam.enabled = false;
        gameObject.SetActive(false);

       
        GameObject fadeObj = new GameObject("DeathFadeHandler");
        DontDestroyOnLoad(fadeObj);
        DeathFadeSequence seq = fadeObj.AddComponent<DeathFadeSequence>();
        seq.Run(fadePanel, cameraFollowTime, fadeDuration, holdBeforeReload);
    }
}