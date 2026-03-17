using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerPortalTraveller : PortalTraveller
{
    CharacterController controller;
    Camera cam;

    [HideInInspector] public Vector3 velocity;
    [HideInInspector] public float yaw;
    [HideInInspector] public float smoothYaw;
    [HideInInspector] public float pitch;
    [HideInInspector] public float smoothPitch;

    void Awake()
    {
        travellerType = PortalTravellerType.Player;
        controller = GetComponent<CharacterController>();
        cam = GetComponentInChildren<Camera>();
    }

    public override void Teleport(Transform fromPortal, Transform toPortal, Vector3 pos, Quaternion rot)
    {
        controller.enabled = false;
        transform.position = pos;

        Vector3 eulerRot = rot.eulerAngles;
        float yawDelta = Mathf.DeltaAngle(smoothYaw, eulerRot.y);
        yaw += yawDelta;
        smoothYaw += yawDelta;
        transform.eulerAngles = Vector3.up * smoothYaw;

        float newPitch = eulerRot.x;
        if (newPitch > 180f) newPitch -= 360f;
        float pitchDelta = newPitch - smoothPitch;
        pitch += pitchDelta;
        smoothPitch += pitchDelta;
        cam.transform.localEulerAngles = Vector3.right * smoothPitch;

        velocity = toPortal.TransformVector(fromPortal.InverseTransformVector(velocity));

        controller.enabled = true;
        Physics.SyncTransforms();
    }
}
