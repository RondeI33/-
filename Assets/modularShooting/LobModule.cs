using System.Collections.Generic;
using UnityEngine;

public class LobModule : MonoBehaviour, IShotModifier
{
    [SerializeField] float lobAngle = 30f;
    [SerializeField] float wallCheckDistance = 4f;

    public List<ShotData> ProcessShots(List<ShotData> shots)
    {
        int myId = GetInstanceID();

        foreach (ShotData shot in shots)
        {
            if (shot.isRaycast) continue;
            if (shot.HasAppliedModifier(myId)) continue;

            shot.MarkModifierApplied(myId);

            
            if (Physics.Raycast(shot.origin, shot.direction.normalized, wallCheckDistance, shot.hitLayers))
                continue;

            shot.SetProperty("useGravity", true);
            float existing = shot.GetProperty("lobAngle", 0f);
            shot.SetProperty("lobAngle", existing + lobAngle);
        }

        return shots;
    }
}