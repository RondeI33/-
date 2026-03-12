using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class SplitShotModule : MonoBehaviour, IShotModifier
{
    [SerializeField] float splitTime = 0.07f;
    [SerializeField] int splitCount = 5;
    [SerializeField] float splitSpread = 15f;
    [SerializeField] float splitDamageMultiplier = 0.6f;

    public List<ShotData> ProcessShots(List<ShotData> shots)
    {
        int myId = GetInstanceID();

        foreach (ShotData shot in shots)
        {
            List<int> splitByIds = shot.GetProperty<List<int>>("splitByIds", null);
            if (splitByIds != null && splitByIds.Contains(myId)) continue;

            if (shot.GetProperty("splitPending", false)) continue;

            float usedTime = shot.GetProperty("splitTimeUsed", 0f);
            float totalTime = usedTime + splitTime;

            shot.SetProperty("splitPending", true);
            shot.SetProperty("splitCount", splitCount);
            shot.SetProperty("splitSpread", splitSpread);
            shot.SetProperty("splitDamageMultiplier", splitDamageMultiplier);
            shot.SetProperty("splitModuleId", myId);
            shot.SetProperty("splitTotalTime", totalTime);
            shot.SetProperty("splitFireTime", Time.time);

            if (shot.isRaycast)
            {
                float originalMaxDistance = shot.maxDistance;
                float rayDist = shot.speed > 0 ? shot.speed * totalTime : totalTime * 100f;
                shot.maxDistance = Mathf.Min(rayDist, originalMaxDistance);

                shot.onPostExecute.Add((HitInfo? hitResult, ShotData data) =>
                {
                    if (hitResult.HasValue) return;

                    int count = data.GetProperty("splitCount", 0);
                    float spread = data.GetProperty("splitSpread", 15f);
                    float mult = data.GetProperty("splitDamageMultiplier", 0.6f);
                    int moduleId = data.GetProperty("splitModuleId", 0);
                    float tt = data.GetProperty("splitTotalTime", 0f);

                    Vector3 splitOrigin = data.origin + data.direction.normalized * data.maxDistance;
                    float remainingDist = originalMaxDistance - data.maxDistance;
                    if (remainingDist <= 0f) return;

                    List<ShotData> fragments = CreateFragments(data, splitOrigin, data.direction, count, spread, mult, remainingDist, moduleId, tt);

                    if (fragments.Count > 0 && data.weaponController != null)
                        data.weaponController.FireSecondary(fragments);
                });
            }
        }

        return shots;
    }

    public static List<ShotData> CreateFragments(ShotData source, Vector3 origin, Vector3 forward, int count, float spread, float dmgMult, float maxDist, int moduleId, float timeUsed)
    {
        List<ShotData> fragments = new List<ShotData>();

        List<int> parentIds = source.GetProperty<List<int>>("splitByIds", null);
        List<int> fragmentIds = parentIds != null ? new List<int>(parentIds) : new List<int>();
        fragmentIds.Add(moduleId);

        Vector3 up = Vector3.up;
        if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.99f)
            up = Vector3.right;

        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(0f, spread * 0.5f);
            float spin = Random.Range(0f, 360f);
            Vector3 randomAxis = Quaternion.AngleAxis(spin, forward) * up;

            ShotData fragment = source.Clone();
            fragment.origin = origin;
            fragment.direction = Quaternion.AngleAxis(angle, randomAxis) * forward;
            fragment.damage = source.damage * dmgMult;
            fragment.maxDistance = maxDist;
            fragment.isRaycast = source.isRaycast;

            fragment.onPostExecute.Clear();
            fragment.properties.Remove("splitPending");
            fragment.properties.Remove("splitCount");
            fragment.properties.Remove("splitSpread");
            fragment.properties.Remove("splitDamageMultiplier");
            fragment.properties.Remove("splitModuleId");
            fragment.properties.Remove("splitTotalTime");
            fragment.SetProperty("splitByIds", new List<int>(fragmentIds));
            fragment.SetProperty("splitTimeUsed", timeUsed);
            fragment.SetProperty("splitFireTime", Time.time);
            fragment.SetProperty("lobAngle", 0f);

            fragments.Add(fragment);
        }

        return fragments;
    }
}