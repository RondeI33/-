using System.Collections.Generic;
using UnityEngine;

public class ProjectileShooter : MonoBehaviour, IFireSource
{
    [SerializeField] GameObject projectilePrefab;
    [SerializeField] float damage = 25f;
    [SerializeField] float speed = 30f;
    [SerializeField] float maxDistance = 200f;
    [SerializeField] float spreadAngle = 3f;

    private WeaponController controller;

    void Awake()
    {
        controller = GetComponent<WeaponController>();
    }

    public List<ShotData> CreateShots(int sourceIndex, int totalSources)
    {
        Transform fp = controller.GetFirePoint();
        Vector3 dir = controller.GetAimDirection();

        if (totalSources > 1 && sourceIndex > 0)
        {
            Vector3 perp = Vector3.Cross(dir, Vector3.up);
            if (perp.sqrMagnitude < 0.01f)
                perp = Vector3.Cross(dir, Vector3.right);
            perp.Normalize();

            float randomAngle = Random.Range(0f, 360f);
            perp = Quaternion.AngleAxis(randomAngle, dir) * perp;
            dir = Quaternion.AngleAxis(spreadAngle, perp) * dir;
        }


        ShotData shot = new ShotData
        {
            origin = fp.position,
            direction = dir,
            damage = damage,
            speed = speed,
            maxDistance = maxDistance,
            isRaycast = false,
            projectilePrefab = projectilePrefab
        };
        return new List<ShotData> { shot };
    }
}