using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class MainMenu : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RoomGenerator roomGenerator;
    [SerializeField] private FPSCamera fpsCamera;
    [SerializeField] private LoadingScreenController loadingScreenController;
    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject optionsPanel;

    [Header("Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private Button backButton;

    [Header("Options")]
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TMP_Text sensitivityValueText;

    [Header("Sound Effects")]
    [SerializeField] private AudioClip hoverSound;
    [SerializeField] private AudioClip clickSound;
    [SerializeField] private AudioClip startClickSound;
    [SerializeField] private AudioClip sliderSound;

    private AudioSource uiAudioSource;
    private AudioSource clickAudioSource;
    private AudioSource startAudioSource;
    private Canvas menuCanvas;

    private void Awake()
    {
        uiAudioSource = gameObject.AddComponent<AudioSource>();
        uiAudioSource.playOnAwake = false;

        clickAudioSource = gameObject.AddComponent<AudioSource>();
        clickAudioSource.playOnAwake = false;

        GameObject startSoundObj = new GameObject("StartSoundPlayer");
        DontDestroyOnLoad(startSoundObj);
        startAudioSource = startSoundObj.AddComponent<AudioSource>();
        startAudioSource.playOnAwake = false;

        menuCanvas = GetComponentInChildren<Canvas>();

        startButton.onClick.AddListener(OnStartClicked);
        optionsButton.onClick.AddListener(OnOptionsClicked);
        exitButton.onClick.AddListener(OnExitClicked);
        backButton.onClick.AddListener(OnBackClicked);

        fullscreenToggle.onValueChanged.AddListener(OnFullscreenToggled);
        sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);

        SetupButtonEvents(startButton, isStartButton: true);
        SetupButtonEvents(optionsButton);
        SetupButtonEvents(exitButton);
        SetupButtonEvents(backButton);

        EventTrigger sliderTrigger = sensitivitySlider.gameObject.AddComponent<EventTrigger>();
        EventTrigger.Entry dragEntry = new EventTrigger.Entry();
        dragEntry.eventID = EventTriggerType.Drag;
        dragEntry.callback.AddListener((data) =>
        {
            if (!uiAudioSource.isPlaying)
                PlayUISound(sliderSound);
        });
        sliderTrigger.triggers.Add(dragEntry);

        optionsPanel.SetActive(false);
        mainMenuPanel.SetActive(true);

        LoadSettings();
    }

    private void Start()
    {
        ShowMenu();
    }

    private void SetupButtonEvents(Button button, bool isStartButton = false)
    {
        EventTrigger trigger = button.gameObject.AddComponent<EventTrigger>();

        EventTrigger.Entry hoverEntry = new EventTrigger.Entry();
        hoverEntry.eventID = EventTriggerType.PointerEnter;
        hoverEntry.callback.AddListener((data) =>
        {
            if (!uiAudioSource.isPlaying)
                PlayUISound(hoverSound);
        });
        trigger.triggers.Add(hoverEntry);

        EventTrigger.Entry clickEntry = new EventTrigger.Entry();
        clickEntry.eventID = EventTriggerType.PointerClick;
        clickEntry.callback.AddListener((data) =>
        {
            if (isStartButton)
                PlayStartSound(startClickSound != null ? startClickSound : clickSound);
            else
                PlayClickSound(clickSound);
        });
        trigger.triggers.Add(clickEntry);
    }

    private void PlayUISound(AudioClip clip)
    {
        if (clip != null && uiAudioSource != null)
        {
            uiAudioSource.clip = clip;
            uiAudioSource.Play();
        }
    }

    private void PlayClickSound(AudioClip clip)
    {
        if (clip != null && clickAudioSource != null)
        {
            clickAudioSource.PlayOneShot(clip);
        }
    }

    private void PlayStartSound(AudioClip clip)
    {
        if (clip != null && startAudioSource != null)
        {
            startAudioSource.clip = clip;
            startAudioSource.Play();
        }
    }

    private void ShowMenu()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;

        if (fpsCamera != null)
            fpsCamera.SetLocked(true);
    }

    private void HideMenu()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Time.timeScale = 1f;

        if (fpsCamera != null)
            fpsCamera.ReloadSensitivity();
    }

    private void LoadSettings()
    {
        float sens = PlayerPrefs.GetFloat("Sensitivity", 6f);
        sensitivitySlider.value = sens;
        sensitivityValueText.text = sens.ToString("F1");

        bool fs = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
        fullscreenToggle.isOn = fs;
        Screen.SetResolution(1920, 1080, fs);
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetFloat("Sensitivity", sensitivitySlider.value);
        PlayerPrefs.SetInt("Fullscreen", fullscreenToggle.isOn ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void OnStartClicked()
    {
        SaveSettings();
        HideMenu();
        if (loadingScreenController != null)
            loadingScreenController.ActivateLoadingVisuals();

        if (MusicManager.Instance != null)
            MusicManager.Instance.StartGameMusic();

        if (roomGenerator != null)
            roomGenerator.StartCoroutine(roomGenerator.Generate());

        menuCanvas.gameObject.SetActive(false);
    }

    private void OnOptionsClicked()
    {
        mainMenuPanel.SetActive(false);
        optionsPanel.SetActive(true);
    }

    private void OnBackClicked()
    {
        SaveSettings();
        optionsPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    private void OnExitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnFullscreenToggled(bool isOn)
    {
        Screen.SetResolution(1920, 1080, isOn);
        if (!uiAudioSource.isPlaying)
            PlayUISound(clickSound);
    }

    private void OnSensitivityChanged(float value)
    {
        sensitivityValueText.text = value.ToString("F1");
    }
}