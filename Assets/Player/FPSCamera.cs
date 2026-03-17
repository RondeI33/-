using UnityEngine;
using UnityEngine.InputSystem;
public class FPSCamera : MonoBehaviour
{
    private float mouseSensitivity = 6f;
    private float minVerticalAngle = -90f;
    private float maxVerticalAngle = 90f;
    private float xRotation = 0f;
    private float sensitivityMultiplier = 1f;
    private bool locked;
    private PlayerInput playerInput;
    private InputAction lookAction;
    private void Awake()
    {
        playerInput = GetComponentInParent<PlayerInput>();
        lookAction = playerInput.actions["Look"];
        Cursor.lockState = CursorLockMode.Locked;
        mouseSensitivity = PlayerPrefs.GetFloat("Sensitivity", 6f);
    }
    private void LateUpdate()
    {
        if (!locked)
            HandleMouseLook();
    }
    private void OnEnable()
    {
        lookAction.Enable();
    }
    private void OnDisable()
    {
        lookAction.Disable();
    }
    public void SetSensitivityMultiplier(float multiplier)
    {
        sensitivityMultiplier = multiplier;
    }
    public void SetLocked(bool value)
    {
        locked = value;
    }
    public void ReloadSensitivity()
    {
        mouseSensitivity = PlayerPrefs.GetFloat("Sensitivity", 6f);
    }
    private void HandleMouseLook()
    {
        Vector2 lookInput = lookAction.ReadValue<Vector2>();
        float mouseX = lookInput.x * mouseSensitivity * sensitivityMultiplier * 0.01f;
        float mouseY = lookInput.y * mouseSensitivity * sensitivityMultiplier * 0.01f;
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, minVerticalAngle, maxVerticalAngle);
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.parent.Rotate(Vector3.up * mouseX);
    }
    public void OnPortalTeleport(float newPitch)
    {
        xRotation = Mathf.Clamp(newPitch, minVerticalAngle, maxVerticalAngle);
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }
}