using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;

public class EscapeMenu : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FPSCamera fpsCamera;
    [SerializeField] private GameObject loadingScreen;

    [Header("Panels")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject optionsPanel;

    [Header("Pause Buttons")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button mainMenuButton;

    [Header("Options")]
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TMP_Text sensitivityValueText;
    [SerializeField] private Button backButton;

    [Header("Sound Effects")]
    [SerializeField] private AudioClip hoverSound;
    [SerializeField] private AudioClip clickSound;
    [SerializeField] private AudioClip sliderSound;

    private PlayerInput playerInput;
    private AudioSource uiAudioSource;
    private AudioSource menuAudioSource;
    private bool isPaused;

    private void Awake()
    {
        playerInput = GetComponentInParent<PlayerInput>();

        uiAudioSource = gameObject.AddComponent<AudioSource>();
        uiAudioSource.playOnAwake = false;

        GameObject menuSoundObj = new GameObject("MenuSoundPlayer");
        DontDestroyOnLoad(menuSoundObj);
        menuAudioSource = menuSoundObj.AddComponent<AudioSource>();
        menuAudioSource.playOnAwake = false;

        resumeButton.onClick.AddListener(Resume);
        optionsButton.onClick.AddListener(OpenOptions);
        mainMenuButton.onClick.AddListener(ReturnToMainMenu);
        backButton.onClick.AddListener(CloseOptions);

        fullscreenToggle.onValueChanged.AddListener(OnFullscreenToggled);
        sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);

        SetupButtonSounds(resumeButton);
        SetupButtonSounds(optionsButton);
        SetupButtonSounds(mainMenuButton);
        SetupButtonSounds(backButton);

        EventTrigger sliderTrigger = sensitivitySlider.gameObject.AddComponent<EventTrigger>();
        EventTrigger.Entry dragEntry = new EventTrigger.Entry();
        dragEntry.eventID = EventTriggerType.Drag;
        dragEntry.callback.AddListener((data) =>
        {
            if (!uiAudioSource.isPlaying)
                PlaySound(sliderSound);
        });
        sliderTrigger.triggers.Add(dragEntry);

        pausePanel.SetActive(false);
        optionsPanel.SetActive(false);
        isPaused = false;
    }

    private void OnEnable()
    {
        playerInput.actions["Escape"].performed += OnEscapePressed;
    }

    private void OnDisable()
    {
        playerInput.actions["Escape"].performed -= OnEscapePressed;
    }

    private void OnEscapePressed(InputAction.CallbackContext ctx)
    {
        if (loadingScreen != null && loadingScreen.activeInHierarchy)
            return;

        if (optionsPanel.activeSelf)
        {
            CloseOptions();
            return;
        }

        if (isPaused)
            Resume();
        else
            Pause();
    }

    private void Pause()
    {
        isPaused = true;
        GamePauser.Pause(playerInput);
        pausePanel.SetActive(true);
        optionsPanel.SetActive(false);

        if (fpsCamera != null)
            fpsCamera.SetLocked(true);
    }

    private void Resume()
    {
        SaveSettings();
        isPaused = false;
        GamePauser.Unpause(playerInput);
        pausePanel.SetActive(false);
        optionsPanel.SetActive(false);

        if (fpsCamera != null)
        {
            fpsCamera.SetLocked(false);
            fpsCamera.ReloadSensitivity();
        }
    }

    private void OpenOptions()
    {
        LoadSettings();
        pausePanel.SetActive(false);
        optionsPanel.SetActive(true);
    }

    private void CloseOptions()
    {
        SaveSettings();
        optionsPanel.SetActive(false);
        pausePanel.SetActive(true);
    }

    private void ReturnToMainMenu()
    {
        SaveSettings();
        if (clickSound != null && menuAudioSource != null)
        {
            menuAudioSource.clip = clickSound;
            menuAudioSource.Play();
        }
        if (MusicManager.Instance != null)
            MusicManager.Instance.ReturnToMenu();
        GamePauser.ForceReset(playerInput);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void LoadSettings()
    {
        float sens = PlayerPrefs.GetFloat("Sensitivity", 6f);
        sensitivitySlider.value = sens;
        sensitivityValueText.text = sens.ToString("F1");

        bool fs = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
        fullscreenToggle.isOn = fs;
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetFloat("Sensitivity", sensitivitySlider.value);
        PlayerPrefs.SetInt("Fullscreen", fullscreenToggle.isOn ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void OnFullscreenToggled(bool isOn)
    {
        Screen.SetResolution(1920, 1080, isOn);
        if (!uiAudioSource.isPlaying)
            PlaySound(clickSound);
    }

    private void OnSensitivityChanged(float value)
    {
        sensitivityValueText.text = value.ToString("F1");
    }

    private void SetupButtonSounds(Button button)
    {
        EventTrigger trigger = button.gameObject.AddComponent<EventTrigger>();

        EventTrigger.Entry hoverEntry = new EventTrigger.Entry();
        hoverEntry.eventID = EventTriggerType.PointerEnter;
        hoverEntry.callback.AddListener((data) =>
        {
            if (!uiAudioSource.isPlaying)
                PlaySound(hoverSound);
        });
        trigger.triggers.Add(hoverEntry);

        EventTrigger.Entry clickEntry = new EventTrigger.Entry();
        clickEntry.eventID = EventTriggerType.PointerClick;
        clickEntry.callback.AddListener((data) => PlaySound(clickSound));
        trigger.triggers.Add(clickEntry);
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && uiAudioSource != null)
            uiAudioSource.PlayOneShot(clip);
    }
}