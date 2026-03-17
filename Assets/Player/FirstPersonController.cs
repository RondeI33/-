using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : PortalTraveller
{
    [Header("Movement Settings")]
    private float walkSpeed = 5.66f;
    private float jumpHeight = 1.433f;
    private float gravity = -13.0473f;
    private float groundFriction = 6.33f;
    private float airControl = 0.8f;
    private float ceilingCheckDistance = 0.5f;

    private CharacterController controller;
    private Vector3 velocity;
    private Vector3 moveDirection;
    private bool isGrounded;
    private Vector2 moveInput;
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction pushAction;
    private float queueSpeed;
    private bool isSpeedQueued = false;
    private bool jumpPressed = false;
    private float currentSlideSpeed = 1.33f;
    private float slideSpeed = 3.66f;
    private bool isSlidingOnSlope = false;
    private Vector3 wallNormal;
    ForceApplier forceApplier;
    private bool hasBounced = false;
    private float bounceCooldown = 0f;
    private float speedMultiplier = 1f;
    FPSCamera fpsCam;
    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();
        forceApplier = GetComponent<ForceApplier>();
        moveAction = playerInput.actions["Move"];
        jumpAction = playerInput.actions["Jump"];
        pushAction = playerInput.actions["Push"];
        travellerType = PortalTravellerType.Player;
        fpsCam = GetComponentInChildren<FPSCamera>();
    }

    public override void Teleport(Transform fromPortal, Transform toPortal, Vector3 pos, Quaternion rot)
    {
        controller.enabled = false;

        Quaternion portalRotDiff = toPortal.rotation * Quaternion.Euler(0f, 180f, 0f) * Quaternion.Inverse(fromPortal.rotation);

        velocity = portalRotDiff * velocity;
        moveDirection = portalRotDiff * moveDirection;

        Vector3 forceVel = forceApplier.GetVelocity();
        if (forceVel.sqrMagnitude > 0.01f)
            forceApplier.SetVelocity(portalRotDiff * forceVel);

        transform.position = pos;

        Vector3 oldForward = transform.forward;
        Vector3 newForward = portalRotDiff * oldForward;
        float newYaw = Mathf.Atan2(newForward.x, newForward.z) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, newYaw, 0f);

        if (fpsCam != null)
        {
            Vector3 oldCamForward = fpsCam.transform.forward;
            Vector3 newCamForward = portalRotDiff * oldCamForward;
            float newPitch = -Mathf.Asin(Mathf.Clamp(newCamForward.y, -1f, 1f)) * Mathf.Rad2Deg;
            fpsCam.OnPortalTeleport(newPitch);
        }

        controller.enabled = true;
        Physics.SyncTransforms();
        lastTeleportTime = Time.time;
    }

    public override void EnterPortalThreshold() { }
    public override void ExitPortalThreshold() { }

    private void Update()
    {
        //HandlePushForce();
        if (jumpAction.triggered)
        {
            jumpPressed = true;
        }


    }

    private void FixedUpdate()
    {
        HandleSlopeSliding();
        HandleGravity();
        MovePlayer();
        SpeedQueue();

    }



    private Vector3 cachedSlideDir;

    private void HandleSlopeSliding()
    {
        isSlidingOnSlope = false;
        RaycastHit hit;
        Vector3 sphereOrigin = transform.position + Vector3.down * 0.33f + controller.center * 1.33f;
        float sphereRadius = 0.5f;
        float maxDistance = 0.5f;
        if (Physics.SphereCast(sphereOrigin, sphereRadius, Vector3.down, out hit, maxDistance, ~(1 << gameObject.layer), QueryTriggerInteraction.Ignore))
        {
            float angle = Vector3.Angle(hit.normal, Vector3.up);
            if (angle > controller.slopeLimit && angle < 89f)
            {
                isSlidingOnSlope = true;
                if (velocity.y > 0) return;
                // Accelerate towards max slide speed
                currentSlideSpeed = Mathf.MoveTowards(currentSlideSpeed, slideSpeed, 13.33f * Time.fixedDeltaTime);
                Vector3 newSlideDir = Vector3.ProjectOnPlane(Vector3.down, hit.normal).normalized;

                // Smooth towards new direction instead of snapping
                if (cachedSlideDir == Vector3.zero)
                    cachedSlideDir = newSlideDir;
                else
                    cachedSlideDir = Vector3.Lerp(cachedSlideDir, newSlideDir, 3f * Time.fixedDeltaTime).normalized;

                controller.Move(cachedSlideDir * currentSlideSpeed * Time.fixedDeltaTime);
                return;
            }
        }
        // Reset slide speed when not on slope
        currentSlideSpeed = 1.33f;
        cachedSlideDir = Vector3.zero;
    }


    private void SpeedQueue()
    {
        if (isSpeedQueued && isGrounded)
        {
            isSpeedQueued = false;
            walkSpeed = queueSpeed;
            queueSpeed = 0;

        }
    }


    private void MovePlayer()
    {
        isGrounded = IsGrounded();
        moveInput = moveAction.ReadValue<Vector2>();

        Vector3 targetMoveDirection = (transform.right * moveInput.x + transform.forward * moveInput.y).normalized;
        float currentSpeed = (isGrounded ? walkSpeed : walkSpeed * airControl) * speedMultiplier;

        if (isGrounded && moveInput.magnitude < 0.1f)
        {
            moveDirection = Vector3.Lerp(moveDirection, Vector3.zero, groundFriction * Time.fixedDeltaTime);
        }
        else if (moveInput.magnitude < 0.1f)
        {
            Vector3 desiredMove = targetMoveDirection * currentSpeed;
            moveDirection = Vector3.Lerp(moveDirection, desiredMove, (isGrounded ? 10f : 0.33f) * Time.fixedDeltaTime);
        }
        else
        {
            Vector3 desiredMove = targetMoveDirection * currentSpeed;
            moveDirection = Vector3.Lerp(moveDirection, desiredMove, (isGrounded ? 10f : 3f) * Time.fixedDeltaTime);
        }

        controller.Move(moveDirection * Time.fixedDeltaTime);

        if (jumpPressed && isGrounded)
        {
            if (!isSlidingOnSlope)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            else
            {
                RaycastHit hit;
                Vector3 sphereOrigin = transform.position + Vector3.down * 0.33f + controller.center * 1.33f;
                if (Physics.SphereCast(sphereOrigin, 0.5f, Vector3.down, out hit, 0.5f, ~(1 << gameObject.layer), QueryTriggerInteraction.Ignore))
                {
                    Vector3 pushDir = Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;
                    moveDirection = pushDir * slideSpeed * 2f;
                }
            }
        }
        jumpPressed = false;

        if (IsCeilingHit())
        {
            if (velocity.y > 0)
            {
                velocity.y = -velocity.y * 0.33f;
            }
        }
    }

    public bool IsGrounded()
    {
        RaycastHit hit;
        Vector3 sphereOrigin = transform.position + Vector3.down * 0.33f + controller.center * 1.33f;
        Vector3 rayDirection = Vector3.down;
        float sphereRadius = 0.5f;
        float maxDistance = 0.5f;
        //Debug.DrawRay(sphereOrigin, rayDirection * (maxDistance + sphereRadius), Color.red);
        if (Physics.SphereCast(sphereOrigin, sphereRadius, rayDirection, out hit, maxDistance, ~(1 << gameObject.layer), QueryTriggerInteraction.Ignore))
        {
            return true;
        }
        return controller.isGrounded;
    }


    private void HandleGravity()
    {
        if (bounceCooldown > 0)
        {
            bounceCooldown -= Time.fixedDeltaTime;
        }

        if (isGrounded && velocity.y < 0 && !isSlidingOnSlope)
        {
            if (forceApplier.GetVelocity().y > 0 && !hasBounced)
            {

                hasBounced = true;
                bounceCooldown = 0.333f;
            }
            else if (bounceCooldown <= 0)
            {

                velocity.y = -2f;
                if (hasBounced)
                {
                    forceApplier.SetVelocity(Vector3.zero);
                    hasBounced = false;
                }
            }
        }

        if (wallNormal != Vector3.zero)
        {

            moveDirection = Vector3.ProjectOnPlane(moveDirection, wallNormal);
            moveDirection.x = Mathf.Clamp(moveDirection.x, -3.33f, 3.33f);
            moveDirection.z = Mathf.Clamp(moveDirection.z, -3.33f, 3.33f);
            wallNormal = Vector3.zero;

        }

        velocity.y += gravity * Time.fixedDeltaTime;
        controller.Move(velocity * Time.fixedDeltaTime);
    }

    private bool IsCeilingHit()
    {
        Vector3 topOfCapsule = transform.position + Vector3.up - Vector3.up * 0.39f;
        int layerMask = ~(1 << gameObject.layer);
        bool hitCeiling = Physics.CheckSphere(topOfCapsule, ceilingCheckDistance, layerMask, QueryTriggerInteraction.Ignore);
        return hitCeiling;
    }
    private void HandlePushForce()
    {

        if (pushAction.WasPressedThisFrame())
        {
            Vector3 pushDirection = -transform.forward;
            Camera playerCamera = GetComponentInChildren<Camera>();

            if (playerCamera)
            {
                pushDirection = -playerCamera.transform.forward;
            }
            forceApplier.AddForce(pushDirection * 10f, ForceMode.Impulse);
        }
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = multiplier;
    }

    public void SetMovementSpeed(float newSpeed)
    {
        if (isGrounded)
        {
            walkSpeed = newSpeed;
        }
        else
        {
            queueSpeed = newSpeed;
            isSpeedQueued = true;
        }
    }

    public void SetVerticalVelocity(float y)
    {
        velocity.y = y;
    }

    public float GetJumpVelocity()
    {
        return Mathf.Sqrt(jumpHeight * -2f * gravity);
    }

    public float GetMovementSpeed()
    {
        return walkSpeed;
    }
}   