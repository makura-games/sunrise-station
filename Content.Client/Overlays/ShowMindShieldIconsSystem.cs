using Content.Shared.Mindshield;
using Content.Shared.Overlays;
using Content.Shared.Standing;
using Content.Shared.StatusIcon.Components;

namespace Content.Client.Overlays;

public sealed partial class ShowMindShieldIconsSystem : EquipmentHudSystem<ShowMindShieldIconsComponent>
{
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private MindShieldSystem _mindShield = default!;

    [SubscribeLocalEvent]
    private void OnGetStatusIconsEvent(Entity<StatusIconComponent> ent, ref GetStatusIconsEvent args)
    {
        // Is active checks for our ability to display status icons
        if (!IsActive)
            return;

        if (_standing.IsDown(ent.Owner)) // Sunrise-Edit - не показываем имплант у лежащих персонажей.
            return;

        _mindShield.GetMindshieldStatus(ent.Owner, out _, out var isVisible);
        if (isVisible && ProtoMan.Resolve(MindShieldSystem.StatusIcon, out var statusIconPrototype))
            args.StatusIcons.Add(statusIconPrototype);
    }
}
