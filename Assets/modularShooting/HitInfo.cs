using UnityEngine;

public struct HitInfo
{
    public Vector3 point;
    public Vector3 normal;
    public Collider collider;

    public HitInfo(Vector3 point, Vector3 normal, Collider collider)
    {
        this.point = point;
        this.normal = normal;
        this.collider = collider;
    }

    public HitInfo(RaycastHit hit)
    {
        point = hit.point;
        normal = hit.normal;
        collider = hit.collider;
    }
}
