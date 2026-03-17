using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyPortalTraveller : PortalTraveller
{
    NavMeshAgent agent;

    void Awake()
    {
        travellerType = PortalTravellerType.Enemy;
        agent = GetComponent<NavMeshAgent>();
    }

    public override void Teleport(Transform fromPortal, Transform toPortal, Vector3 pos, Quaternion rot)
    {
        Vector3 savedVelocity = agent.velocity;
        Vector3 savedDestination = agent.hasPath ? agent.destination : Vector3.zero;
        bool hadPath = agent.hasPath;

        agent.enabled = false;
        transform.position = pos;
        transform.rotation = rot;
        agent.enabled = true;

        agent.Warp(pos);

        Quaternion portalRotDiff = toPortal.rotation * Quaternion.Euler(0f, 180f, 0f) * Quaternion.Inverse(fromPortal.rotation);
        agent.velocity = portalRotDiff * savedVelocity;

        if (hadPath)
            agent.SetDestination(savedDestination);

        Physics.SyncTransforms();
        lastTeleportTime = Time.time;
    }

    public void TeleportViaNavMeshLink(Transform fromPortal, Transform toPortal)
    {
        Vector3 savedVelocity = agent.velocity;

        Matrix4x4 flipMatrix = Matrix4x4.Rotate(Quaternion.Euler(0f, 180f, 0f));
        var m = toPortal.localToWorldMatrix * flipMatrix * fromPortal.worldToLocalMatrix * transform.localToWorldMatrix;
        Vector3 newPos = m.GetColumn(3);
        Quaternion newRot = m.rotation;

        agent.CompleteOffMeshLink();

        agent.Warp(newPos);
        transform.rotation = newRot;

        Quaternion portalRotDiff2 = toPortal.rotation * Quaternion.Euler(0f, 180f, 0f) * Quaternion.Inverse(fromPortal.rotation);
        agent.velocity = portalRotDiff2 * savedVelocity;
        lastTeleportTime = Time.time;
    }
}
