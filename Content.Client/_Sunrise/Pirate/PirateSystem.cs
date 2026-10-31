using Content.Shared._Sunrise.Pirate;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Sunrise.Pirate;

public sealed partial class PirateSystem : EntitySystem
{

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PirateIconComponent, GetStatusIconsEvent>(GetPirateIcon);
    }

    private void GetPirateIcon(EntityUid uid, PirateIconComponent component, ref GetStatusIconsEvent args)
    {
        var iconPrototype = ProtoMan.Index(component.StatusIcon);
        args.StatusIcons.Add(iconPrototype);
    }
}
