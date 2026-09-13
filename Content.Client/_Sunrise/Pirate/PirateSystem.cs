using Content.Shared._Sunrise.Pirate;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Sunrise.Pirate;

public sealed partial class PirateSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PirateIconComponent, GetStatusIconsEvent>(GetPirateIcon);
    }

    private void GetPirateIcon(EntityUid uid, PirateIconComponent component, ref GetStatusIconsEvent args)
    {
        var iconPrototype = _prototype.Index(component.StatusIcon);
        args.StatusIcons.Add(iconPrototype);
    }
}
