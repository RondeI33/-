using System.Collections.Generic;
using UnityEngine;

public class LobModule : MonoBehaviour, IShotModifier
{
    [SerializeField] float lobAngle = 30f;

    public List<ShotData> ProcessShots(List<ShotData> shots)
    {
        int myId = GetInstanceID();

        foreach (ShotData shot in shots)
        {
            if (shot.isRaycast) continue;
            if (shot.HasAppliedModifier(myId)) continue;

            shot.MarkModifierApplied(myId);
            shot.SetProperty("useGravity", true);

            float existing = shot.GetProperty("lobAngle", 0f);
            shot.SetProperty("lobAngle", existing + lobAngle);
        }

        return shots;
    }
}
