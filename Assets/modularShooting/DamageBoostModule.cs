using System.Collections.Generic;
using UnityEngine;

public class DamageBoostModule : MonoBehaviour, IShotModifier
{
    [SerializeField] float flatBonus = 0f;
    [SerializeField] float multiplier = 1f;

    public List<ShotData> ProcessShots(List<ShotData> shots)
    {
        int myId = GetInstanceID();

        foreach (ShotData shot in shots)
        {
            if (shot.HasAppliedModifier(myId)) continue;
            shot.MarkModifierApplied(myId);

            shot.damage = (shot.damage + flatBonus) * multiplier;
        }

        return shots;
    }
}
