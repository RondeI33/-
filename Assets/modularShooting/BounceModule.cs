using System.Collections.Generic;
using UnityEngine;

public class BounceModule : MonoBehaviour, IShotModifier
{
    [SerializeField] int maxBounces = 3;
    [SerializeField] bool applyEffectsOnBounce = false;

    public List<ShotData> ProcessShots(List<ShotData> shots)
    {
        int myId = GetInstanceID();

        foreach (ShotData shot in shots)
        {
            if (shot.HasAppliedModifier(myId)) continue;
            shot.MarkModifierApplied(myId);

            int existing = shot.GetProperty("maxBounces", 0);
            shot.SetProperty("maxBounces", existing + maxBounces + 1);
            shot.SetProperty("bouncesLeft", existing + maxBounces + 1);
            shot.SetProperty("applyEffectsOnBounce", applyEffectsOnBounce);
        }

        return shots;
    }
}