using UnityEngine;
using UnityEngine.InputSystem;

public class Zoom : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float zoomFOV = 30f;
    [SerializeField] private float zoomSpeed = 10f;
    [SerializeField] private GameObject crosshair;
    [SerializeField] private GameObject zoomCrosshair;
    [SerializeField] private float crosshairZoomScale = 0.5f;
    [SerializeField] private FPSCamera fpsCamera;
    [SerializeField] private CoinThrow coinThrow;
    [SerializeField] private ShotgunDash shotgunDash;

    private FirstPersonController playerController;
    private PlayerInput playerInput;
    private InputAction zoomAction;
    private float defaultFOV;
    private Vector3 defaultCrosshairScale;
    private HitPopUp hitPopUp;

    private void Start()
    {
        playerController = GetComponentInParent<FirstPersonController>();
        playerInput = GetComponentInParent<PlayerInput>();
        zoomAction = playerInput.actions["Zoom"];
        defaultFOV = playerCamera.fieldOfView;
        defaultCrosshairScale = crosshair.transform.localScale;
        zoomCrosshair.SetActive(false);
        hitPopUp = FindFirstObjectByType<HitPopUp>();
    }

    private void Update()
    {
        bool coinBusy = coinThrow != null && coinThrow.IsBusy();
        bool shotgunActive = shotgunDash != null && shotgunDash.IsDashActive;
        bool zooming = zoomAction.IsPressed() && !coinBusy && !shotgunActive;

        float baseFOV = zooming ? zoomFOV : defaultFOV;
        float buffOffset = coinThrow != null ? coinThrow.GetBuffFovOffset() : 0f;
        float targetFOV = baseFOV + buffOffset;

        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFOV, Time.unscaledDeltaTime * zoomSpeed);

        float ratio = playerCamera.fieldOfView / defaultFOV;
        float sensMultiplier = Mathf.MoveTowards(1f, ratio, 0.77f);
        fpsCamera.SetSensitivityMultiplier(sensMultiplier);

        Vector3 targetScale = zooming ? defaultCrosshairScale * crosshairZoomScale : defaultCrosshairScale;
        float hitMultiplier = hitPopUp != null ? hitPopUp.GetCrosshairScaleMultiplier() : 1f;
        crosshair.transform.localScale = Vector3.Lerp(crosshair.transform.localScale, targetScale * hitMultiplier, Time.unscaledDeltaTime * zoomSpeed);

        zoomCrosshair.SetActive(zooming);

        if (zooming)
            playerController.SetMovementSpeed(playerController.GetMovementSpeed() * 0.553f);
    }

    public bool IsZooming() => zoomAction.IsPressed() && (coinThrow == null || !coinThrow.IsBusy()) && (shotgunDash == null || !shotgunDash.IsDashActive);
}