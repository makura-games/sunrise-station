using Content.Shared.Examine;
using Content.Shared.Ghost;
using Content.Shared.Warps;

namespace Content.Shared.Warps;

public sealed class WarpPointSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WarpPointComponent, ExaminedEvent>(OnWarpPointExamine);
    }

    private void OnWarpPointExamine(EntityUid uid, WarpPointComponent component, ExaminedEvent args)
    {
        if (!HasComp<GhostComponent>(args.Examiner))
            return;

        var location = component.Location ?? Name(uid);
        var locationKey = $"location-{location.Replace(" ", "-")}";

        if (Loc.TryGetString(locationKey, out var localizedLocation)) // Sunrise-Edit
            location = localizedLocation;

        args.PushText(Loc.GetString("warp-point-component-on-examine-success", ("location", location)));
    }
}
