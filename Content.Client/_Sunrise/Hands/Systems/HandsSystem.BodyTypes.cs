using Content.Shared._Sunrise.Humanoid;
using Content.Shared.DisplacementMap;
using Content.Shared.Hands.Components;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Client.Hands.Systems;

public sealed partial class HandsSystem
{
    public void RefreshSunriseHandVisuals(Entity<HandsComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        UpdateAllHandVisuals((ent.Owner, ent.Comp));
    }

    private DisplacementData? GetSunriseHandDisplacement(
        EntityUid uid,
        HandLocation location,
        HandsComponent hands)
    {
        if (TryComp<SunriseHumanoidProfileComponent>(uid, out var profile) &&
            ProtoMan.TryIndex(profile.BodyType, out var bodyType))
        {
            var bodyTypeKey = bodyType.VisualKey;

            if (location == HandLocation.Left &&
                hands.LeftHandBodyTypeDisplacements.TryGetValue(bodyTypeKey, out var left))
            {
                return left;
            }

            if (location == HandLocation.Right &&
                hands.RightHandBodyTypeDisplacements.TryGetValue(bodyTypeKey, out var right))
            {
                return right;
            }

            if (hands.BodyTypeDisplacements.TryGetValue(bodyTypeKey, out var generic))
                return generic;
        }

        return location switch
        {
            HandLocation.Left => hands.LeftHandDisplacement,
            HandLocation.Right => hands.RightHandDisplacement,
            _ => hands.HandDisplacement,
        };
    }
}
