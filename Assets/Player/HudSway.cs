using UnityEngine;
using UnityEngine.InputSystem;

public class HudSway : MonoBehaviour
{
    public float bobSpeed = 10f;
    public float bobAmountX = 3f;
    public float bobAmountY = 5f;
    public float jumpBounceAmount = 15f;
    public float landBounceAmount = 12f;
    public float bounceSpeed = 10f;
    public float crouchOffset = 10f;
    public float breathSpeed = 2f;
    public float breathAmount = 2f;
    public float smoothSpeed = 8f;
    public float tiltAmount = 2f;

    private float landCooldown = 0.2f;
    private PlayerInput playerInput;
    private FirstPersonController playerController;
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction crouchAction;

    private RectTransform[] children;
    private Vector2[] childInitialPositions;
    private float bobTimer;
    private float breathTimer;
    private float bounceOffset;
    private float currentCrouchOffset;
    private bool canLand;
    private bool wasGrounded;
    private float landCooldownTimer;
    private Vector2 currentOffset;
    private float currentTilt;

    void Start()
    {
        playerInput = FindFirstObjectByType<PlayerInput>();
        playerController = playerInput.GetComponent<FirstPersonController>();
        moveAction = playerInput.actions["Move"];
        jumpAction = playerInput.actions["Jump"];
        crouchAction = playerInput.actions["Crouch"];

        int count = transform.childCount;
        children = new RectTransform[count];
        childInitialPositions = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            children[i] = transform.GetChild(i).GetComponent<RectTransform>();
            childInitialPositions[i] = children[i].anchoredPosition;
        }

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

        Vector2 targetOffset;

        if (input.magnitude > 0.1f)
        {
            bobTimer += Time.deltaTime * bobSpeed;
            breathTimer = 0f;
            float bobY = Mathf.Sin(bobTimer) * bobAmountY;
            float bobX = Mathf.Sin(bobTimer * 0.5f) * bobAmountX;
            targetOffset = new Vector2(bobX, bobY + bounceOffset + currentCrouchOffset);
        }
        else
        {
            bobTimer = 0f;
            breathTimer += Time.deltaTime * breathSpeed;
            float breathY = Mathf.Sin(breathTimer) * breathAmount;
            targetOffset = new Vector2(0f, breathY + bounceOffset + currentCrouchOffset);
        }

        currentOffset = Vector2.Lerp(currentOffset, targetOffset, Time.deltaTime * smoothSpeed);

        float targetTilt = input.x * -tiltAmount;
        currentTilt = Mathf.Lerp(currentTilt, targetTilt, Time.deltaTime * smoothSpeed);

        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] == null) continue;
            children[i].anchoredPosition = childInitialPositions[i] + currentOffset;
            children[i].localEulerAngles = new Vector3(0f, 0f, currentTilt);
        }
    }
}