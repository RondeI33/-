using UnityEngine;
using UnityEngine.InputSystem;

public static class GamePauser
{
    private static int pauseCount = 0;

    public static bool IsPaused => pauseCount > 0;

    public static void Pause(PlayerInput playerInput)
    {
        pauseCount++;
        Time.timeScale = 0f;
        playerInput.actions.FindActionMap("Player").Disable();
        playerInput.actions["Inventory"].Enable();
        playerInput.actions["Escape"].Enable();
        playerInput.actions["Pause"].Enable();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public static void Unpause(PlayerInput playerInput)
    {
        pauseCount = Mathf.Max(0, pauseCount - 1);
        if (pauseCount > 0) return;

        Time.timeScale = 1f;
        playerInput.actions.FindActionMap("Player").Enable();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public static void ForceReset(PlayerInput playerInput)
    {
        pauseCount = 0;
        Time.timeScale = 1f;
        playerInput.actions.FindActionMap("Player").Enable();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}