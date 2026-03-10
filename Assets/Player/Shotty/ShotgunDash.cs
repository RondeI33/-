using UnityEngine;
using UnityEngine.InputSystem;
public class ShotgunDash : MonoBehaviour
{
    [SerializeField] private float dashForce = 22f;
    [SerializeField] private float dashCooldown = 1.1f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float upwardBias = 3f;
    [SerializeField] private AudioSource dashAudioSource;
    [SerializeField] private AudioClip dashSound;
    [SerializeField] private float pitchMin = 0.9f;
    [SerializeField] private float pitchMax = 1.1f;
    private FirstPersonController playerController;
    private ForceApplier forceApplier;
    private PlayerInput playerInput;
    private InputAction zoomAction;
    private CoinThrow coinThrow;
    private float cooldownTimer = 0f;
    private bool isDashing = false;
    private float dashTimer = 0f;
    private Vector3 dashDir;
    private void Start()
    {
        playerController = GetComponentInParent<FirstPersonController>();
        forceApplier = GetComponentInParent<ForceApplier>();
        playerInput = GetComponentInParent<PlayerInput>();
        coinThrow = GetComponentInParent<CoinThrow>();
        zoomAction = playerInput.actions["Zoom"];
    }
    private void OnEnable()
    {
        isDashing = false;
        dashTimer = 0f;
        cooldownTimer = 0f;
    }
    private void OnDisable()
    {
        if (isDashing)
        {
            isDashing = false;
            dashTimer = 0f;
            forceApplier.SetVelocity(Vector3.zero);
        }
        cooldownTimer = 0f;
    }
    private void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;
        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f)
                isDashing = false;
            return;
        }
        if (zoomAction.WasPressedThisFrame() && cooldownTimer <= 0f)
            TriggerDash();
    }
    private void TriggerDash()
    {
        bool buffed = coinThrow != null && coinThrow.IsBuffActive;
        Vector2 moveInput = playerInput.actions["Move"].ReadValue<Vector2>();
        dashDir = (playerController.transform.right * moveInput.x + playerController.transform.forward * moveInput.y);
        dashDir.y = 0f;
        if (dashDir.sqrMagnitude < 0.01f)
            dashDir = playerController.transform.forward;
        dashDir.Normalize();
        float force = buffed ? dashForce * 2f : dashForce;
        float cooldown = buffed ? 0.1f : dashCooldown;
        forceApplier.SetVelocity(dashDir * force + Vector3.up * upwardBias);
        isDashing = true;
        dashTimer = dashDuration;
        cooldownTimer = cooldown;
        PlayDashSound();
    }
    private void PlayDashSound()
    {
        if (dashAudioSource == null || dashSound == null) return;
        dashAudioSource.pitch = Random.Range(pitchMin, pitchMax);
        dashAudioSource.clip = dashSound;
        dashAudioSource.Play();
    }
    public bool IsDashActive => gameObject.activeInHierarchy;
}