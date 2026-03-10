using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(FirstPersonController))]
public class Crouch : MonoBehaviour
{
    [Header("Crouch Settings")]
    private float crouchHeight = 1.33f;
    private float crouchSpeed = 3.13f;
    private float crouchTransitionRatio = 0.1f;
    private float crouchColliderOffset = 0.335f;
    private float standCheckHeight = 0.66f;

    private CharacterController controller;
    private FirstPersonController fpsMovement;
    private PlayerInput playerInput;
    private InputAction crouchAction;

    private float originalHeight;
    private float originalCenterY;
    private float originalSpeed;
    private float targetCrouchCenterY;
    private bool isCrouching = false;

    public bool IsCrouching => isCrouching;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        fpsMovement = GetComponent<FirstPersonController>();
        playerInput = GetComponent<PlayerInput>();
        crouchAction = playerInput.actions["Crouch"];

        originalHeight = controller.height;
        originalCenterY = controller.center.y;
        originalSpeed = fpsMovement.GetMovementSpeed();
        crouchSpeed = originalSpeed * 0.553f;
        targetCrouchCenterY = (originalCenterY * crouchHeight) + crouchColliderOffset;
    }

    private void Update()
    {
        isCrouching = crouchAction.IsPressed();
        float transitionSpeed = crouchTransitionRatio * 60f;

        if (isCrouching)
        {
            controller.skinWidth = Mathf.MoveTowards(controller.skinWidth, 0.0733f, transitionSpeed * Time.deltaTime);

            if (Mathf.Abs(controller.height - crouchHeight) > 0.01f)
            {
                controller.height = Mathf.MoveTowards(controller.height, crouchHeight, transitionSpeed * Time.deltaTime);
                controller.center = new Vector3(
                    controller.center.x,
                    Mathf.MoveTowards(controller.center.y, targetCrouchCenterY, transitionSpeed * Time.deltaTime),
                    controller.center.z
                );
            }
            else
            {
                controller.height = crouchHeight;
                controller.center = new Vector3(
                    controller.center.x,
                    targetCrouchCenterY,
                    controller.center.z
                );
            }

            fpsMovement.SetMovementSpeed(crouchSpeed);
        }
        else
        {
            if (CanStandUp())
            {
                controller.height = Mathf.MoveTowards(controller.height, originalHeight, transitionSpeed * Time.deltaTime);
                controller.skinWidth = 0.01f;
                controller.center = new Vector3(
                    controller.center.x,
                    Mathf.MoveTowards(controller.center.y, originalCenterY, transitionSpeed * Time.deltaTime),
                    controller.center.z
                );

                if (controller.center.y < originalCenterY + 0.01f)
                {
                    controller.center = new Vector3(
                        controller.center.x,
                        originalCenterY,
                        controller.center.z
                    );
                }

                if (controller.height < originalHeight - 0.01f && controller.height > originalHeight - 0.05f)
                {
                    controller.height = originalHeight;
                }

                if (controller.height > originalHeight - 0.1f)
                {
                    fpsMovement.SetMovementSpeed(originalSpeed);
                }
            }
        }
    }

    private bool CanStandUp()
    {
        Vector3 topPoint = transform.position + Vector3.up * (controller.height - standCheckHeight) - Vector3.up * 0.5f;
        int layerMask = ~(1 << gameObject.layer);
        return !Physics.CheckSphere(topPoint, 0.5f, layerMask);
    }
}