using System.Collections.Generic;
using UnityEngine;

public class Portal : MonoBehaviour
{
    [Header("Main Settings")]
    public Portal linkedPortal;
    public MeshRenderer screen;
    public MeshRenderer screen2;
    public int recursionLimit = 5;

    [Header("Traversal Settings")]
    public bool allowPlayer = true;
    public bool allowEnemies = true;
    public bool allowProjectiles = true;
    public bool allowPhysicsObjects = true;

    [Header("NavMesh")]
    public bool createNavMeshLink = true;

    [Header("Advanced Settings")]
    public float nearClipOffset = 0.05f;
    public float nearClipLimit = 0.2f;

    static List<Portal> activePortals = new List<Portal>();
    static readonly Matrix4x4 flipMatrix = Matrix4x4.Rotate(Quaternion.Euler(0f, 180f, 0f));

    RenderTexture viewTexture;
    Camera portalCam;
    Camera playerCam;
    List<PortalTraveller> trackedTravellers;
    MeshFilter screenMeshFilter;

    public bool IsLinked => linkedPortal != null;

    void Awake()
    {
        playerCam = Camera.main;
        portalCam = GetComponentInChildren<Camera>();
        portalCam.enabled = false;
        trackedTravellers = new List<PortalTraveller>();
        screenMeshFilter = screen.GetComponent<MeshFilter>();
        screen.material.SetInt("displayMask", 1);
        if (screen2 != null)
            screen2.material = screen.material;

        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null) box.isTrigger = true;

        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.layer = 2;
            if (child != transform)
            {
                Collider col = child.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }
        }
    }

    bool IsFrontSide(Vector3 pos)
    {
        return Vector3.Dot(pos - transform.position, transform.forward) < 0f;
    }

    void OnEnable() { activePortals.Add(this); }
    void OnDisable() { activePortals.Remove(this); }

    void LateUpdate()
    {
        if (!IsLinked) return;
        HandleTravellers();

    }

    void HandleTravellers()
    {
        for (int i = 0; i < trackedTravellers.Count; i++)
        {
            PortalTraveller traveller = trackedTravellers[i];
            Transform travellerT = traveller.transform;

            if (Time.time - traveller.lastTeleportTime < 0.01f)
            {
                trackedTravellers.RemoveAt(i);
                i--;
                continue;
            }

            var m = linkedPortal.transform.localToWorldMatrix * flipMatrix * transform.worldToLocalMatrix * travellerT.localToWorldMatrix;

            Vector3 referencePos = traveller.travellerType == PortalTravellerType.Player
                ? playerCam.transform.position
                : travellerT.position;

            Vector3 offsetFromPortal = referencePos - transform.position;
            int portalSide = System.Math.Sign(Vector3.Dot(offsetFromPortal, transform.forward));
            int portalSideOld = System.Math.Sign(Vector3.Dot(traveller.previousOffsetFromPortal, transform.forward));

            if (portalSide != portalSideOld)
            {
                traveller.Teleport(transform, linkedPortal.transform, m.GetColumn(3), m.rotation);
                if (traveller.graphicsClone != null)
                    traveller.ExitPortalThreshold();
                trackedTravellers.RemoveAt(i);
                i--;
            }
            else
            {
                if (traveller.graphicsClone != null)
                    traveller.graphicsClone.transform.SetPositionAndRotation(m.GetColumn(3), m.rotation);
                traveller.previousOffsetFromPortal = offsetFromPortal;
            }
        }
    }

    public void PrePortalRender()
    {
        foreach (var traveller in trackedTravellers)
        {
            UpdateSliceParams(traveller);
        }
    }

    public void Render()
    {
        if (!IsLinked) return;

        float distToThis = (playerCam.transform.position - transform.position).sqrMagnitude;
        float distToLinked = (playerCam.transform.position - linkedPortal.transform.position).sqrMagnitude;
        bool playerNearby = distToThis < 25f || distToLinked < 25f;

        if (!playerNearby && !CameraUtility.VisibleFromCamera(linkedPortal.screen, playerCam)) return;

        CreateViewTexture();

        var localToWorldMatrix = playerCam.transform.localToWorldMatrix;
        var renderPositions = new Vector3[recursionLimit];
        var renderRotations = new Quaternion[recursionLimit];

        int startIndex = 0;
        portalCam.projectionMatrix = playerCam.projectionMatrix;

        for (int i = 0; i < recursionLimit; i++)
        {
            if (i > 0)
            {
                if (!CameraUtility.BoundsOverlap(screenMeshFilter, linkedPortal.screenMeshFilter, portalCam))
                    break;
            }

            localToWorldMatrix = transform.localToWorldMatrix * flipMatrix * linkedPortal.transform.worldToLocalMatrix * localToWorldMatrix;
            int renderOrderIndex = recursionLimit - i - 1;
            renderPositions[renderOrderIndex] = localToWorldMatrix.GetColumn(3);
            renderRotations[renderOrderIndex] = localToWorldMatrix.rotation;
            portalCam.transform.SetPositionAndRotation(renderPositions[renderOrderIndex], renderRotations[renderOrderIndex]);
            startIndex = renderOrderIndex;
        }

        screen.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;

        for (int i = startIndex; i < recursionLimit; i++)
        {
            portalCam.transform.SetPositionAndRotation(renderPositions[i], renderRotations[i]);
            SetNearClipPlane();
            HandleClipping();

            if (i == startIndex)
                linkedPortal.screen.enabled = true;
            else
                linkedPortal.screen.enabled = true;

            portalCam.Render();
        }

        linkedPortal.screen.enabled = true;

        screen.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
    }

    void HandleClipping()
    {
        const float hideDst = -1000;
        const float showDst = 1000;
        float screenThickness = linkedPortal.ProtectScreenFromClipping(portalCam.transform.position);

        foreach (var traveller in trackedTravellers)
        {
            if (!traveller.HasGraphics) continue;

            if (SameSideOfPortal(traveller.transform.position, portalCamPos))
                traveller.SetSliceOffsetDst(hideDst, false);
            else
                traveller.SetSliceOffsetDst(showDst, false);

            int cloneSideOfLinkedPortal = -SideOfPortal(traveller.transform.position);
            bool camSameSideAsClone = linkedPortal.SideOfPortal(portalCamPos) == cloneSideOfLinkedPortal;
            if (camSameSideAsClone)
                traveller.SetSliceOffsetDst(screenThickness, true);
            else
                traveller.SetSliceOffsetDst(-screenThickness, true);
        }

        foreach (var linkedTraveller in linkedPortal.trackedTravellers)
        {
            if (!linkedTraveller.HasGraphics) continue;

            var travellerPos = linkedTraveller.graphicsObject.transform.position;
            bool cloneOnSameSideAsCam = linkedPortal.SideOfPortal(travellerPos) != SideOfPortal(portalCamPos);
            if (cloneOnSameSideAsCam)
                linkedTraveller.SetSliceOffsetDst(hideDst, true);
            else
                linkedTraveller.SetSliceOffsetDst(showDst, true);

            bool camSameSideAsTraveller = linkedPortal.SameSideOfPortal(linkedTraveller.transform.position, portalCamPos);
            if (camSameSideAsTraveller)
                linkedTraveller.SetSliceOffsetDst(screenThickness, false);
            else
                linkedTraveller.SetSliceOffsetDst(-screenThickness, false);
        }
    }

    public void PostPortalRender()
    {
        foreach (var traveller in trackedTravellers)
        {
            UpdateSliceParams(traveller);
        }
        ProtectScreenFromClipping(playerCam.transform.position);
    }

    void CreateViewTexture()
    {
        if (viewTexture == null || viewTexture.width != Screen.width || viewTexture.height != Screen.height)
        {
            RenderTexture oldTexture = viewTexture;

            viewTexture = new RenderTexture(Screen.width, Screen.height, 24);
            portalCam.targetTexture = viewTexture;
            linkedPortal.screen.material.SetTexture("_MainTex", viewTexture);

            if (oldTexture != null)
                oldTexture.Release();
        }
    }

    float ProtectScreenFromClipping(Vector3 viewPoint)
    {
        float halfHeight = playerCam.nearClipPlane * Mathf.Tan(playerCam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float halfWidth = halfHeight * playerCam.aspect;
        float dstToNearClipPlaneCorner = new Vector3(halfWidth, halfHeight, playerCam.nearClipPlane).magnitude;
        float screenThickness = dstToNearClipPlaneCorner;

        Transform screenT = screen.transform;
        bool camFacingSameDirAsPortal = Vector3.Dot(transform.forward, transform.position - viewPoint) > 0;
        screenT.localScale = new Vector3(screenT.localScale.x, screenT.localScale.y, screenThickness);
        screenT.localPosition = Vector3.forward * screenThickness * (camFacingSameDirAsPortal ? 0.5f : -0.5f);
        return screenThickness;
    }

    void UpdateSliceParams(PortalTraveller traveller)
    {
        if (!traveller.HasGraphics) return;

        int side = SideOfPortal(traveller.transform.position);
        Vector3 sliceNormal = transform.forward * -side;
        Vector3 cloneSliceNormal = linkedPortal.transform.forward * side;

        Vector3 slicePos = transform.position;
        Vector3 cloneSlicePos = linkedPortal.transform.position;

        float sliceOffsetDst = 0;
        float cloneSliceOffsetDst = 0;
        float screenThickness = screen.transform.localScale.z;

        bool playerSameSideAsTraveller = SameSideOfPortal(playerCam.transform.position, traveller.transform.position);
        if (!playerSameSideAsTraveller)
            sliceOffsetDst = -screenThickness;

        bool playerSameSideAsCloneAppearing = side != linkedPortal.SideOfPortal(playerCam.transform.position);
        if (!playerSameSideAsCloneAppearing)
            cloneSliceOffsetDst = -screenThickness;

        if (traveller.originalMaterials == null || traveller.cloneMaterials == null) return;

        for (int i = 0; i < traveller.originalMaterials.Length; i++)
        {
            traveller.originalMaterials[i].SetVector("sliceCentre", slicePos);
            traveller.originalMaterials[i].SetVector("sliceNormal", sliceNormal);
            traveller.originalMaterials[i].SetFloat("sliceOffsetDst", sliceOffsetDst);

            traveller.cloneMaterials[i].SetVector("sliceCentre", cloneSlicePos);
            traveller.cloneMaterials[i].SetVector("sliceNormal", cloneSliceNormal);
            traveller.cloneMaterials[i].SetFloat("sliceOffsetDst", cloneSliceOffsetDst);
        }
    }

    void SetNearClipPlane()
    {
        Transform clipPlane = transform;
        int dot = System.Math.Sign(Vector3.Dot(clipPlane.forward, transform.position - portalCam.transform.position));

        Vector3 camSpacePos = portalCam.worldToCameraMatrix.MultiplyPoint(clipPlane.position);
        Vector3 camSpaceNormal = portalCam.worldToCameraMatrix.MultiplyVector(clipPlane.forward) * dot;
        float camSpaceDst = -Vector3.Dot(camSpacePos, camSpaceNormal) + nearClipOffset;

        if (Mathf.Abs(camSpaceDst) > nearClipLimit)
        {
            Vector4 clipPlaneCameraSpace = new Vector4(camSpaceNormal.x, camSpaceNormal.y, camSpaceNormal.z, camSpaceDst);
            portalCam.projectionMatrix = playerCam.CalculateObliqueMatrix(clipPlaneCameraSpace);
        }
        else
        {
            portalCam.projectionMatrix = playerCam.projectionMatrix;
        }
    }

    void OnTravellerEnterPortal(PortalTraveller traveller)
    {
        if (!trackedTravellers.Contains(traveller))
        {
            traveller.EnterPortalThreshold();
            Vector3 refPos = traveller.travellerType == PortalTravellerType.Player
                ? playerCam.transform.position
                : traveller.transform.position;
            traveller.previousOffsetFromPortal = refPos - transform.position;
            trackedTravellers.Add(traveller);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        var traveller = other.GetComponent<PortalTraveller>();
        if (traveller == null) return;
        if (!CanTraverse(traveller)) return;
        if (Time.time - traveller.lastTeleportTime < 0.01f) return;
        OnTravellerEnterPortal(traveller);
    }

    void OnTriggerStay(Collider other)
    {
        var traveller = other.GetComponent<PortalTraveller>();
        if (traveller == null) return;
        if (trackedTravellers.Contains(traveller)) return;
        if (!CanTraverse(traveller)) return;
        if (Time.time - traveller.lastTeleportTime < 0.01f) return;
        OnTravellerEnterPortal(traveller);
    }

    void OnTriggerExit(Collider other)
    {
        var traveller = other.GetComponent<PortalTraveller>();
        if (traveller && trackedTravellers.Contains(traveller))
        {
            traveller.ExitPortalThreshold();
            trackedTravellers.Remove(traveller);
        }
    }

    public bool CanTraverse(PortalTraveller traveller)
    {
        return traveller.travellerType switch
        {
            PortalTravellerType.Player => allowPlayer,
            PortalTravellerType.Enemy => allowEnemies,
            PortalTravellerType.Projectile => allowProjectiles,
            PortalTravellerType.PhysicsObject => allowPhysicsObjects,
            _ => true
        };
    }

    public static bool TryPassThrough(PortalTraveller traveller, Vector3 from, Vector3 to)
    {
        return TryPassThrough(traveller, from, to, out _);
    }

    public static bool TryPassThrough(PortalTraveller traveller, Vector3 from, Vector3 to, out float crossDistance)
    {
        crossDistance = 0f;

        for (int i = 0; i < activePortals.Count; i++)
        {
            Portal p = activePortals[i];
            if (!p.IsLinked) continue;
            if (!p.CanTraverse(traveller)) continue;
            if (Time.time - traveller.lastTeleportTime < 0.01f) continue;

            Vector3 portalPos = p.transform.position;
            Vector3 portalNormal = p.transform.forward;

            float d1 = Vector3.Dot(from - portalPos, portalNormal);
            float d2 = Vector3.Dot(to - portalPos, portalNormal);

            if (d1 >= 0f || d2 < 0f) continue;

            float t = d1 / (d1 - d2);
            Vector3 intersection = Vector3.Lerp(from, to, t);

            Vector3 screenScale = p.screen.transform.lossyScale;
            float maxPortalRadius = Mathf.Max(screenScale.x, screenScale.y) + 1f;
            float flatDist = Vector3.ProjectOnPlane(intersection - portalPos, portalNormal).magnitude;
            if (flatDist > maxPortalRadius)
                continue;

            crossDistance = Vector3.Distance(from, intersection);

            var m = p.linkedPortal.transform.localToWorldMatrix * flipMatrix * p.transform.worldToLocalMatrix * traveller.transform.localToWorldMatrix;
            traveller.Teleport(p.transform, p.linkedPortal.transform, m.GetColumn(3), m.rotation);
            return true;
        }
        return false;
    }

    int SideOfPortal(Vector3 pos)
    {
        return System.Math.Sign(Vector3.Dot(pos - transform.position, transform.forward));
    }

    bool SameSideOfPortal(Vector3 posA, Vector3 posB)
    {
        return SideOfPortal(posA) == SideOfPortal(posB);
    }

    Vector3 portalCamPos => portalCam.transform.position;

    void OnValidate()
    {
        if (linkedPortal != null)
            linkedPortal.linkedPortal = this;
    }
}