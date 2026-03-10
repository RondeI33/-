using UnityEngine;
using UnityEngine.AI;

public class EnemyForceApplier : MonoBehaviour
{
    [SerializeField][Range(0f, 1f)] private float decelerationFactor = 0.05f;
    [SerializeField] private float mass = 1f;

    private NavMeshAgent agent;
    private Transform moveTarget;
    private Vector3 velocity;
    private bool knocked;

    public bool IsKnocked => knocked;

    private NavMeshAgent FindAgent()
    {
        Transform current = transform;
        while (current != null)
        {
            NavMeshAgent found = current.GetComponent<NavMeshAgent>();
            if (found != null) return found;
            current = current.parent;
        }
        return null;
    }

    private void Update()
    {
        if (!knocked) return;

        velocity.y = 0f;

        if (velocity.magnitude < 0.05f)
        {
            velocity = Vector3.zero;
            knocked = false;
            if (agent != null)
            {
                agent.enabled = true;
                agent.Warp(moveTarget.position);
            }
            return;
        }

        Vector3 displacement = velocity * Time.deltaTime;
        moveTarget.position += displacement;

        velocity *= 1f - Mathf.Pow(1f - decelerationFactor, Time.deltaTime * 60f);
    }

    public void AddForce(Vector3 force, ForceMode mode = ForceMode.Impulse)
    {
        if (agent == null)
        {
            agent = FindAgent();
            moveTarget = agent != null ? agent.transform : transform;
        }

        force.y = 0f;

        switch (mode)
        {
            case ForceMode.Impulse:
                velocity += force / mass;
                break;
            case ForceMode.VelocityChange:
                velocity += force;
                break;
            case ForceMode.Force:
                velocity += (force / mass) * Time.fixedDeltaTime;
                break;
            case ForceMode.Acceleration:
                velocity += force * Time.fixedDeltaTime;
                break;
        }

        if (!knocked)
        {
            knocked = true;
            if (agent != null)
            {
                agent.ResetPath();
                agent.enabled = false;
            }
        }
    }
}