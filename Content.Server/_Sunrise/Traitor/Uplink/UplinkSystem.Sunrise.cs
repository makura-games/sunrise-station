using Content.Shared.FixedPoint;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server.Traitor.Uplink;

public sealed partial class UplinkSystem
{
    [Dependency] private TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> SunriseUplinkTag = "SunriseUplink";

    private bool ShouldSkipSunriseUplink(EntityUid? uplink)
    {
        return uplink is { } uid && _tag.HasTag(uid, SunriseUplinkTag);
    }

    public EntityUid? FindUplinkByTag(EntityUid user, ProtoId<TagPrototype> tag)
    {
        if (_inventorySystem.TryGetContainerSlotEnumerator(user, out var containerSlotEnumerator))
        {
            while (containerSlotEnumerator.MoveNext(out var containerSlot))
            {
                if (containerSlot.ContainedEntity is not { } uplink)
                    continue;

                if (_tag.HasTag(uplink, tag))
                    return uplink;
            }
        }

        foreach (var item in _handsSystem.EnumerateHeld(user))
        {
            if (_tag.HasTag(item, tag))
                return item;
        }

        return null;
    }

    public void SetSunriseUplink(EntityUid user, EntityUid store, FixedPoint2 balance, bool giveDiscounts)
    {
        SetUplink(user, store, balance, giveDiscounts);
    }
}
