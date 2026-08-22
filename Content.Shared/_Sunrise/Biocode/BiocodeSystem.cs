using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Biocode;

public sealed partial class BiocodeSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BiocodeComponent, AttemptThrowBiocodeEvent>(OnAttemptThrowBiocode);
    }

    public bool CanUse(EntityUid user, HashSet<ProtoId<NpcFactionPrototype>> factions)
    {
        var canUse = false;
        if (!TryComp<NpcFactionMemberComponent>(user, out var npcFactionMemberComponent))
            return canUse;

        foreach (var faction in npcFactionMemberComponent.Factions)
        {
            if (factions.Contains(faction))
                canUse = true;
        }

        return canUse;
    }

    private void OnAttemptThrowBiocode(EntityUid uid, BiocodeComponent component, ref AttemptThrowBiocodeEvent args)
    {
        if (args.User == null || CanUse(args.User.Value, component.Factions))
            return;

        if (!string.IsNullOrEmpty(component.AlertText))
            _popup.PopupEntity(Loc.GetString(component.AlertText), args.User.Value, args.User.Value);

        args.Cancelled = true;
    }

}
