using UnityEngine;

public class ZubrHitbox : MonoBehaviour
{
    private BossZubr boss;

    private void Awake()
    {
        boss = GetComponentInParent<BossZubr>();
    }

    public void TakeDamage(float damage)
    {
        if (boss != null)
            boss.TakeZubrDamage(damage);
    }

    public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (boss != null)
            boss.TakeZubrDamage(damage, hitPoint, hitNormal);
    }
}