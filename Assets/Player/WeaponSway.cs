using UnityEngine;
using UnityEngine.InputSystem;
public class WeaponSway : MonoBehaviour
{
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction crouchAction;
    private FirstPersonController playerController;
    private WeaponRecoil weaponRecoil;
    private Quaternion initialRotation;
    public float tiltAmount = 5f;
    public float smoothSpeed = 8f;
    public float bobSpeed = 10f;
    public float bobAmount = 0.02f;
    public float jumpBounceAmount = 0.16f;
    public float landBounceAmount = 0.13f;
    public float bounceSpeed = 10f;
    public float crouchOffset = 0.13f;
    public float breathSpeed = 2f;
    public float breathAmount = 0.004f;
    public Vector3 breathAxis = new Vector3(0f, 1f, 0f);
    public enum ForwardAxis { Vertical, Horizontal }
    [SerializeField] private ForwardAxis forwardTiltAxis = ForwardAxis.Vertical;
    [SerializeField] private bool reverseForwardTilt = false;
    [SerializeField] private bool reverseSideTilt = false;
    private float landCooldown = 0.2f;
    private float bobTimer = 0f;
    private float breathTimer = 0f;
    private Vector3 initialPosition;
    private float bounceOffset = 0f;
    private bool canLand = false;
    private bool wasGrounded = true;
    private float landCooldownTimer = 0f;
    private float currentCrouchOffset = 0f;
    private Vector3 lastRecoilOffset;
    private Quaternion lastRecoilRotation = Quaternion.identity;
    void Start()
    {
        playerInput = GetComponentInParent<PlayerInput>();
        playerController = GetComponentInParent<FirstPersonController>();
        weaponRecoil = GetComponent<WeaponRecoil>();
        moveAction = playerInput.actions["Move"];
        jumpAction = playerInput.actions["Jump"];
        crouchAction = playerInput.actions["Crouch"];
        initialRotation = transform.localRotation;
        initialPosition = transform.localPosition;
        wasGrounded = playerController.IsGrounded();
    }
    void Update()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();
        bool isGrounded = playerController.IsGrounded();
        if (landCooldownTimer > 0f)
            landCooldownTimer -= Time.deltaTime;
        if (jumpAction.WasPressedThisFrame() && isGrounded)
        {
            bounceOffset = jumpBounceAmount;
            canLand = true;
            landCooldownTimer = landCooldown;
        }
        if (wasGrounded && !isGrounded)
            canLand = true;
        if (canLand && isGrounded && landCooldownTimer <= 0f)
        {
            bounceOffset = -landBounceAmount;
            canLand = false;
        }
        wasGrounded = isGrounded;
        bounceOffset = Mathf.Lerp(bounceOffset, 0f, Time.deltaTime * bounceSpeed);
        float targetCrouchOffset = crouchAction.IsPressed() ? -crouchOffset : 0f;
        currentCrouchOffset = Mathf.Lerp(currentCrouchOffset, targetCrouchOffset, Time.deltaTime * smoothSpeed);
        transform.localRotation = transform.localRotation * Quaternion.Inverse(lastRecoilRotation);
        float forwardInput = forwardTiltAxis == ForwardAxis.Vertical ? input.y : input.x;
        float sideInput = forwardTiltAxis == ForwardAxis.Vertical ? input.x : input.y;
        float tiltX = forwardInput * tiltAmount * (reverseForwardTilt ? 1f : -1f);
        float tiltZ = sideInput * tiltAmount * (reverseSideTilt ? -1f : 1f);
        Quaternion targetRotation = initialRotation * Quaternion.Euler(tiltX, 0f, tiltZ);
        transform.localRotation = Quaternion.Lerp(transform.localRotation, targetRotation, Time.deltaTime * smoothSpeed);
        Quaternion recoilRot = weaponRecoil ? weaponRecoil.GetRecoilRotation() : Quaternion.identity;
        transform.localRotation = transform.localRotation * recoilRot;
        lastRecoilRotation = recoilRot;
        transform.localPosition -= lastRecoilOffset;
        Vector3 recoilOffset = weaponRecoil ? weaponRecoil.GetRecoilOffset() : Vector3.zero;
        if (input.magnitude > 0.1f)
        {
            bobTimer += Time.deltaTime * bobSpeed;
            breathTimer = 0f;
            float bobY = Mathf.Sin(bobTimer) * bobAmount;
            float bobX = Mathf.Sin(bobTimer * 0.5f) * bobAmount * 0.5f;
            Vector3 targetPos = initialPosition + new Vector3(bobX, bobY + bounceOffset + currentCrouchOffset, 0f);
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, Time.deltaTime * smoothSpeed);
        }
        else
        {
            bobTimer = 0f;
            breathTimer += Time.deltaTime * breathSpeed;
            float breath = Mathf.Sin(breathTimer) * breathAmount;
            Vector3 targetPos = initialPosition + new Vector3(
                breathAxis.x * breath,
                breathAxis.y * breath + bounceOffset + currentCrouchOffset,
                breathAxis.z * breath
            );
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, Time.deltaTime * smoothSpeed);
        }
        transform.localPosition += recoilOffset;
        lastRecoilOffset = recoilOffset;
    }
}