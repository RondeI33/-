using UnityEngine;

public interface IEnemy : IDamageable
{
    void InitAgent();
    void Activate();
    void SetDoors(Doors room);
    void TakeDamage(float damage);
    void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitNormal);

    void IDamageable.TakeDamage(float damage, HitInfo hitInfo)
    {
        TakeDamage(damage, hitInfo.point, hitInfo.normal);
    }
}