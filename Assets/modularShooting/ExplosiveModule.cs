using System.Collections.Generic;
using UnityEngine;

public class ExplosiveModule : MonoBehaviour, IShotModifier
{
    [SerializeField] float explosionRadius = 5f;
    [SerializeField] float explosionDamage = 15f;
    [SerializeField] GameObject explosionEffectPrefab;

    public List<ShotData> ProcessShots(List<ShotData> shots)
    {
        int myId = GetInstanceID();
        float radius = explosionRadius;
        float dmg = explosionDamage;
        GameObject fx = explosionEffectPrefab;

        foreach (ShotData shot in shots)
        {
            if (shot.HasAppliedModifier(myId)) continue;
            shot.MarkModifierApplied(myId);

            float existingRadius = shot.GetProperty("explosionRadius", 0f);
            float existingDmg = shot.GetProperty("explosionDamage", 0f);
            shot.SetProperty("explosionRadius", existingRadius + radius);
            shot.SetProperty("explosionDamage", existingDmg + dmg);

            if (existingRadius == 0f)
            {
                shot.onHitCallbacks.Add((HitInfo info, ShotData data) =>
                {
                    float r = data.GetProperty("explosionRadius", 0f);
                    float d = data.GetProperty("explosionDamage", 0f);

                    if (fx != null)
                    {
                        GameObject fxInstance = Instantiate(fx, info.point, Quaternion.identity);
                        ParticleSystem ps = fxInstance.GetComponent<ParticleSystem>();
                        if (ps != null)
                        {
                            var main = ps.main;
                            main.startSize = r * 2f;
                        }
                    }

                    Collider[] hits = Physics.OverlapSphere(info.point, r, data.hitLayers);
                    foreach (Collider col in hits)
                    {
                        if (col == info.collider) continue;
                        IDamageable target = col.GetComponentInParent<IDamageable>();
                        if (target != null)
                        {
                            Vector3 closestPoint = col.ClosestPoint(info.point);
                            Vector3 normal = (closestPoint - info.point).normalized;
                            target.TakeDamage(d, new HitInfo(closestPoint, normal, col));
                            data.weaponController?.ShowHitFeedback(col, true);          //change to false or change the hit sound 
                        }
                    }
                });
            }
        }
        return shots;
    }
}