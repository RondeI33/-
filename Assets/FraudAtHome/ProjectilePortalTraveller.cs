using UnityEngine;

public class ProjectilePortalTraveller : PortalTraveller
{
    Rigidbody rb;

    [HideInInspector] public Vector3 moveDirection;
    [HideInInspector] public float moveSpeed;
    [HideInInspector] public bool usesRigidbody;

    void Awake()
    {
        travellerType = PortalTravellerType.Projectile;
        rb = GetComponent<Rigidbody>();
        usesRigidbody = rb != null;
    }

    public override void Teleport(Transform fromPortal, Transform toPortal, Vector3 pos, Quaternion rot)
    {
        transform.position = pos;
        transform.rotation = rot;

        if (usesRigidbody && rb != null)
        {
            rb.linearVelocity = toPortal.TransformVector(fromPortal.InverseTransformVector(rb.linearVelocity));
            rb.angularVelocity = toPortal.TransformVector(fromPortal.InverseTransformVector(rb.angularVelocity));
        }

        moveDirection = toPortal.TransformDirection(fromPortal.InverseTransformDirection(moveDirection));
    }

    public override void EnterPortalThreshold()
    {
        if (graphicsClone == null && graphicsObject != null)
        {
            graphicsClone = Instantiate(graphicsObject);
            graphicsClone.transform.parent = graphicsObject.transform.parent;
            graphicsClone.transform.localScale = graphicsObject.transform.localScale;
            originalMaterials = GetMaterialsSafe(graphicsObject);
            cloneMaterials = GetMaterialsSafe(graphicsClone);
        }
        else if (graphicsClone != null)
        {
            graphicsClone.SetActive(true);
        }
    }

    public override void ExitPortalThreshold()
    {
        if (graphicsClone != null)
            graphicsClone.SetActive(false);

        if (originalMaterials != null)
        {
            for (int i = 0; i < originalMaterials.Length; i++)
                originalMaterials[i].SetVector("sliceNormal", Vector3.zero);
        }
    }

    Material[] GetMaterialsSafe(GameObject g)
    {
        if (g == null) return new Material[0];
        var renderers = g.GetComponentsInChildren<MeshRenderer>();
        var matList = new System.Collections.Generic.List<Material>();
        foreach (var renderer in renderers)
        {
            foreach (var mat in renderer.materials)
                matList.Add(mat);
        }
        return matList.ToArray();
    }

    void OnDisable()
    {
        if (graphicsClone != null)
            graphicsClone.SetActive(false);
    }
}
