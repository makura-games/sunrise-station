using Content.Shared.Emag.Systems;
using Content.Shared.Mind;
using Content.Shared.Overlays;
using Content.Shared.Popups;
using Content.Shared._Sunrise.Silicons.Laws.Components;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Stunnable;
using Content.Shared.Wires;
using Robust.Shared.Audio;

namespace Content.Shared.Silicons.Laws;

/// <summary>
/// This handles getting and displaying the laws for silicons.
/// </summary>
public abstract partial class SharedSiliconLawSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stunSystem = default!;
    [Dependency] private EmagSystem _emag = default!;
    [Dependency] private SharedMindSystem _mind = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        InitializeUpdater();
        SubscribeLocalEvent<EmagSiliconLawComponent, GotEmaggedEvent>(OnGotEmagged);
    }

    private void OnGotEmagged(EntityUid uid, EmagSiliconLawComponent component, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        if (_emag.CheckFlag(uid, EmagType.Interaction))
            return;

        // prevent self-emagging
        if (uid == args.UserUid)
        {
            _popup.PopupClient(Loc.GetString("law-emag-cannot-emag-self"), uid, args.UserUid);
            return;
        }

        if (component.RequireOpenPanel &&
            TryComp<WiresPanelComponent>(uid, out var panel) &&
            !panel.Open)
        {
            _popup.PopupClient(Loc.GetString("law-emag-require-panel"), uid, args.UserUid);
            return;
        }

        var ev = new SiliconEmaggedEvent(args.UserUid, args.EmagUid); // Sunrise Edit
        RaiseLocalEvent(uid, ref ev);

        component.OwnerName = Name(args.UserUid);

        // Sunrise-Start - FreeMAG has its own distinctive sound, so don't stack the default emagged borg cue on top.
        var cue = component.EmaggedSound;
        if (args.EmagUid is { } emagUid &&
            TryComp<LawsetEmagComponent>(emagUid, out var lawsetEmag))
        {
            cue = lawsetEmag.EmaggedSound;
        }

        NotifyLawsChanged(uid, cue);
        // Sunrise-End
        if(_mind.TryGetMind(uid, out var mindId, out _))
            EnsureSubvertedSiliconRole(mindId);

        _stunSystem.TryUpdateParalyzeDuration(uid, component.StunTime);

        args.Handled = true;
    }

    public virtual void NotifyLawsChanged(EntityUid uid, SoundSpecifier? cue = null)
    {

    }

    protected virtual void EnsureSubvertedSiliconRole(EntityUid mindId)
    {
        if (TryComp<MindComponent>(mindId, out var mind))
        {
            var owner = mind.OwnedEntity;
            if (TryComp<ShowCrewIconsComponent>(owner, out var crewIconComp))
            {
                crewIconComp.UncertainCrewBorder = true;
                Dirty(owner.Value, crewIconComp);
            }
        }
    }

    protected virtual void RemoveSubvertedSiliconRole(EntityUid mindId)
    {
        if (TryComp<MindComponent>(mindId, out var mind))
        {
            var owner = mind.OwnedEntity;
            if (TryComp<ShowCrewIconsComponent>(owner, out var crewIconComp))
            {
                crewIconComp.UncertainCrewBorder = false;
                Dirty(owner.Value, crewIconComp);
            }
        }
    }
}

[ByRefEvent]
public record struct SiliconEmaggedEvent(EntityUid user, EntityUid? EmagUid = null); // Sunrise Edit
