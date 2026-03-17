using UnityEngine;

public class PortalSpawner : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] GameObject portalPrefab;

    [Header("Placement")]
    public float maxPlaceDistance = 50f;
    public LayerMask placementMask;
    public float portalSurfaceOffset = 0.01f;

    Portal portalA;
    Portal portalB;
    bool nextIsA = true;

    PortalRenderManager renderManager;

    void Awake()
    {
        renderManager = FindFirstObjectByType<PortalRenderManager>();
    }

    public Portal SpawnPortal(Vector3 position, Vector3 surfaceNormal)
    {
        GameObject go = Instantiate(portalPrefab, position + surfaceNormal * portalSurfaceOffset, Quaternion.LookRotation(surfaceNormal));
        Portal portal = go.GetComponent<Portal>();
        return portal;
    }

    public void SpawnPortalFromRaycast(Ray ray)
    {
        if (!Physics.Raycast(ray, out RaycastHit hit, maxPlaceDistance, placementMask))
            return;

        if (nextIsA)
        {
            if (portalA != null)
                Destroy(portalA.gameObject);

            portalA = SpawnPortal(hit.point, hit.normal);
        }
        else
        {
            if (portalB != null)
                Destroy(portalB.gameObject);

            portalB = SpawnPortal(hit.point, hit.normal);
        }

        if (portalA != null && portalB != null)
            LinkPortals(portalA, portalB);

        nextIsA = !nextIsA;

        if (renderManager != null)
            renderManager.RefreshPortals();
    }

    public void LinkPortals(Portal a, Portal b)
    {
        a.linkedPortal = b;
        b.linkedPortal = a;

        PortalNavMeshLink linkA = a.GetComponent<PortalNavMeshLink>();
        PortalNavMeshLink linkB = b.GetComponent<PortalNavMeshLink>();

        if (linkA != null) linkA.RebuildLink();
        if (linkB != null) linkB.RebuildLink();
    }

    public void SpawnPrelinkedPair(Vector3 posA, Vector3 normalA, Vector3 posB, Vector3 normalB)
    {
        Portal a = SpawnPortal(posA, normalA);
        Portal b = SpawnPortal(posB, normalB);
        LinkPortals(a, b);

        if (renderManager != null)
            renderManager.RefreshPortals();
    }
}
