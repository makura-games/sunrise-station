using Content.Server.Antag;
using Content.Server.Objectives;
using Content.Server.Popups;
using Content.Shared._Sunrise.Implants;
using Content.Shared._Sunrise.Implants.Components;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Robust.Shared.Player;

namespace Content.Server._Sunrise.Implants;

public sealed class MindControlSystem : SharedMindControlSystem
{
    private const string FollowOrdersObjectiveId = "MindControlledFollowOrders";

    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ObjectivesSystem _objectives = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly AntagSelectionSystem _antag = default!;

    protected override bool CanApplyMindControl(Entity<MindControlImplantComponent> ent, EntityUid implanted, string masterName)
    {
        return HasComp<ActorComponent>(implanted);
    }

    protected override void SendImplantedBriefing(Entity<MindControlImplantComponent> ent, EntityUid implanted, string masterName)
    {
        if (!TryComp<ActorComponent>(implanted, out var actor))
            return;

        _antag.SendBriefing(actor.PlayerSession,
            Loc.GetString(ent.Comp.BriefingText, ("master-name", masterName)),
            null,
            ent.Comp.GreetSoundNotification);
    }

    protected override void SendRemovedBriefing(Entity<MindControlImplantComponent> ent, EntityUid implanted)
    {
        if (TryComp<ActorComponent>(implanted, out var actor))
            _antag.SendBriefing(actor.PlayerSession, Loc.GetString(ent.Comp.DebriefingText), null, null);
    }

    protected override void RemoveObjective(EntityUid uid)
    {
        if (!_mind.TryGetMind(uid, out var mindId, out var mind))
            return;

        if (_mind.TryFindObjective((mindId, mind), FollowOrdersObjectiveId, out var objective) &&
            objective != null &&
            mind.Objectives.IndexOf(objective.Value) is var index &&
            index >= 0)
        {
            _mind.TryRemoveObjective(mindId, mind, index);
        }

        _popup.PopupEntity(Loc.GetString("mind-control-user-freed"), uid, uid, PopupType.Medium);
    }

    protected override void AssignObjective(EntityUid uid)
    {
        if (!_mind.TryGetMind(uid, out var mindId, out var mind))
            return;

        var objective = _objectives.TryCreateObjective(mindId, mind, FollowOrdersObjectiveId);
        if (objective == null)
            return;

        _mind.AddObjective(mindId, mind, objective.Value);
    }
}
