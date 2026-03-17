using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

[RequireComponent(typeof(Portal))]
public class PortalNavMeshLink : MonoBehaviour
{
    [Header("Link Settings")]
    public float linkWidth = 1f;
    public int agentTypeID = 0;
    public float linkEndOffset = 0.5f;

    Portal portal;
    NavMeshLink navMeshLink;
    List<NavMeshAgent> agentsInTrigger = new List<NavMeshAgent>();

    void Start()
    {
        portal = GetComponent<Portal>();
        if (portal.createNavMeshLink && portal.IsLinked)
        {
            CreateLink();
        }
    }

    void CreateLink()
    {
        if (navMeshLink != null) return;

        navMeshLink = gameObject.AddComponent<NavMeshLink>();

        Vector3 startLocal = Vector3.zero - transform.forward * linkEndOffset;
        Vector3 endWorld = portal.linkedPortal.transform.position + portal.linkedPortal.transform.forward * linkEndOffset;
        Vector3 endLocal = transform.InverseTransformPoint(endWorld);

        navMeshLink.startPoint = startLocal;
        navMeshLink.endPoint = endLocal;
        navMeshLink.width = linkWidth;
        navMeshLink.bidirectional = true;
        navMeshLink.agentTypeID = agentTypeID;
        navMeshLink.autoUpdate = false;
    }

    public void RebuildLink()
    {
        if (navMeshLink != null)
            Destroy(navMeshLink);
        navMeshLink = null;

        if (portal.createNavMeshLink && portal.IsLinked)
            CreateLink();
    }

    void Update()
    {
        for (int i = agentsInTrigger.Count - 1; i >= 0; i--)
        {
            if (agentsInTrigger[i] == null || !agentsInTrigger[i].gameObject.activeInHierarchy)
            {
                agentsInTrigger.RemoveAt(i);
                continue;
            }

            NavMeshAgent agent = agentsInTrigger[i];
            if (!agent.isOnOffMeshLink) continue;

            EnemyPortalTraveller traveller = agent.GetComponent<EnemyPortalTraveller>();
            if (traveller == null) continue;

            Vector3 agentPos = agent.transform.position;
            float dotToPortal = Vector3.Dot(agentPos - transform.position, transform.forward);
            if (Mathf.Abs(dotToPortal) < 0.3f)
            {
                traveller.TeleportViaNavMeshLink(transform, portal.linkedPortal.transform);
                agentsInTrigger.RemoveAt(i);
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        NavMeshAgent agent = other.GetComponent<NavMeshAgent>();
        if (agent != null && !agentsInTrigger.Contains(agent))
        {
            agent.autoTraverseOffMeshLink = false;
            agentsInTrigger.Add(agent);
        }
    }

    void OnTriggerExit(Collider other)
    {
        NavMeshAgent agent = other.GetComponent<NavMeshAgent>();
        if (agent != null)
            agentsInTrigger.Remove(agent);
    }

    void OnDestroy()
    {
        if (navMeshLink != null)
            Destroy(navMeshLink);
    }
}
