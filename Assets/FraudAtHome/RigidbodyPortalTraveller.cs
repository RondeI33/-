using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class RigidbodyPortalTraveller : PortalTraveller
{
    new Rigidbody rigidbody;

    void Awake()
    {
        travellerType = PortalTravellerType.PhysicsObject;
        rigidbody = GetComponent<Rigidbody>();
    }

    public override void Teleport(Transform fromPortal, Transform toPortal, Vector3 pos, Quaternion rot)
    {
        base.Teleport(fromPortal, toPortal, pos, rot);
        Quaternion portalRotDiff = toPortal.rotation * Quaternion.Euler(0f, 180f, 0f) * Quaternion.Inverse(fromPortal.rotation);
        rigidbody.linearVelocity = portalRotDiff * rigidbody.linearVelocity;
        rigidbody.angularVelocity = portalRotDiff * rigidbody.angularVelocity;
    }
}
