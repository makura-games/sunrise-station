using Content.Shared._Sunrise.FleshCult;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;
namespace Content.Client._Sunrise.FleshCult;

public sealed partial class FleshCultistSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FleshCultistComponent, GetStatusIconsEvent>(GetFleshCultistIcon);
        SubscribeLocalEvent<FleshMobComponent, GetStatusIconsEvent>(GetFleshMobIcon);
    }

    private void GetFleshCultistIcon(Entity<FleshCultistComponent> ent, ref GetStatusIconsEvent args)
    {
        var iconPrototype = _prototype.Index(ent.Comp.StatusIcon);
        args.StatusIcons.Add(iconPrototype);
    }

    private void GetFleshMobIcon(Entity<FleshMobComponent> ent, ref GetStatusIconsEvent args)
    {
        if (HasComp<FleshCultistComponent>(ent))
            return;

        var iconPrototype = _prototype.Index(ent.Comp.StatusIcon);
        args.StatusIcons.Add(iconPrototype);
    }
}
