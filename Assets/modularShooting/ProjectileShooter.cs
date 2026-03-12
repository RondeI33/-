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

        if (totalSources > 1)
        {
            float t = (sourceIndex / (float)(totalSources - 1)) * 2f - 1f;
            float angle = t * spreadAngle * (totalSources - 1) * 0.5f;
            dir = Quaternion.AngleAxis(angle, Vector3.up) * dir;
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