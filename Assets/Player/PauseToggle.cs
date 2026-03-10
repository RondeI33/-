using UnityEngine;
using UnityEngine.InputSystem;

public class PauseToggle : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    private PlayerInput playerInput;
    private bool isPaused = false;

    public bool IsPaused => isPaused;

    void Awake()
    {
        playerInput = GetComponentInParent<PlayerInput>();
        pausePanel.SetActive(false);
    }

    void OnEnable()
    {
        playerInput.actions["Pause"].performed += OnPausePressed;
    }

    void OnDisable()
    {
        playerInput.actions["Pause"].performed -= OnPausePressed;
    }

    private void OnPausePressed(InputAction.CallbackContext ctx)
    {
        isPaused = !isPaused;
        if (isPaused)
        {
            GamePauser.Pause(playerInput);
            pausePanel.SetActive(true);
        }
        else
        {
            GamePauser.Unpause(playerInput);
            pausePanel.SetActive(false);
        }
    }
}