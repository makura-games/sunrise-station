using Content.Shared._Sunrise.AssaultOps;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Sunrise.AssaultOps.AssaultOps;

public sealed partial class AssaultOpsSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AssaultOperativeComponent, GetStatusIconsEvent>(GetAssaultOperativeIcon);
    }

    private void GetAssaultOperativeIcon(EntityUid uid, AssaultOperativeComponent component, ref GetStatusIconsEvent args)
    {
        var iconPrototype = _prototype.Index(component.StatusIcon);
        args.StatusIcons.Add(iconPrototype);
    }
}
