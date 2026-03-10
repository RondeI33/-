using UnityEngine;

public interface IEnemy
{
    void InitAgent();
    void Activate();
    void SetDoors(Doors room);
    void TakeDamage(float damage);
    void TakeDamage(float damage, Vector3 a, Vector3 b);
}