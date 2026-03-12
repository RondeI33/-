using System.Collections.Generic;
using UnityEngine;

public class ChainLightningModule : MonoBehaviour, IShotModifier
{
    [SerializeField] float chainRadius = 8f;
    [SerializeField] int maxChains = 3;
    [SerializeField] float chainDamageMultiplier = 0.7f;
    [SerializeField] GameObject chainEffectPrefab;

    public List<ShotData> ProcessShots(List<ShotData> shots)
    {
        int myId = GetInstanceID();
        float radius = chainRadius;
        int chains = maxChains;
        float dmgMult = chainDamageMultiplier;
        GameObject fx = chainEffectPrefab;

        foreach (ShotData shot in shots)
        {
            if (shot.HasAppliedModifier(myId)) continue;
            shot.MarkModifierApplied(myId);

            int existingChains = shot.GetProperty("maxChains", 0);
            shot.SetProperty("maxChains", existingChains + chains);
            shot.SetProperty("chainRadius", radius);
            shot.SetProperty("chainDamageMultiplier", dmgMult);

            if (existingChains == 0)
            {
                shot.onHitCallbacks.Add((HitInfo info, ShotData data) =>
                {
                    if (data.recursionDepth >= data.maxRecursionDepth) return;

                    int chainsLeft = data.GetProperty("maxChains", 0);
                    if (chainsLeft <= 0) return;

                    float r = data.GetProperty("chainRadius", 8f);
                    float mult = data.GetProperty("chainDamageMultiplier", 0.7f);

                    Collider[] nearby = Physics.OverlapSphere(info.point, r, data.hitLayers);
                    List<ShotData> chainShots = new List<ShotData>();
                    int chained = 0;

                    foreach (Collider col in nearby)
                    {
                        if (col == info.collider) continue;
                        if (chained >= chainsLeft) break;

                        IDamageable target = col.GetComponentInParent<IDamageable>();
                        if (target == null) continue;

                        Vector3 targetPoint = col.ClosestPoint(info.point);
                        Vector3 dir = (targetPoint - info.point).normalized;

                        if (fx != null)
                            Instantiate(fx, info.point, Quaternion.LookRotation(dir));

                        ShotData chainShot = new ShotData
                        {
                            origin = info.point,
                            direction = dir,
                            damage = data.damage * mult,
                            maxDistance = r,
                            isRaycast = true,
                            recursionDepth = data.recursionDepth + 1,
                            maxRecursionDepth = data.maxRecursionDepth
                        };

                        chainShots.Add(chainShot);
                        chained++;
                    }

                    if (chainShots.Count > 0 && data.weaponController != null)
                        data.weaponController.FireSecondary(chainShots);
                });
            }
        }

        return shots;
    }
}
