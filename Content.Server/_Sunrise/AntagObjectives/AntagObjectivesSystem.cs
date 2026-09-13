using Content.Server.Administration.Managers;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared._Sunrise.AntagObjectives;
using Content.Shared.Administration;
using Content.Shared.Objectives;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Systems;

namespace Content.Server._Sunrise.AntagObjectives;

public sealed partial class AntagObjectivesSystem : EntitySystem
{
    [Dependency] private MindSystem _minds = default!;
    [Dependency] private RoleSystem _roles = default!;
    [Dependency] private SharedObjectivesSystem _objectives = default!;
    [Dependency] private IAdminManager _admins = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<RequestAntagObjectivesEvent>(OnRequestCharacterInfoEvent);
    }

    private void OnRequestCharacterInfoEvent(RequestAntagObjectivesEvent msg, EntitySessionEventArgs args)
    {
        if (!_admins.HasAdminFlag(args.SenderSession, AdminFlags.Admin))
            return;

        var entity = GetEntity(msg.NetEntity);

        var objectives = new Dictionary<string, List<ObjectiveInfo>>();
        string? briefing = null;
        if (_minds.TryGetMind(entity, out var mindId, out var mind))
        {
            foreach (var objective in mind.Objectives)
            {
                var info = _objectives.GetInfo(objective, mindId, mind);
                if (info == null)
                    continue;

                var issuer = Comp<ObjectiveComponent>(objective).LocIssuer;
                if (!objectives.ContainsKey(issuer))
                    objectives[issuer] = new List<ObjectiveInfo>();
                objectives[issuer].Add(info.Value);
            }

            briefing = _roles.MindGetBriefing(mindId);
        }

        RaiseNetworkEvent(new AntagObjectivesEvent(objectives, briefing), args.SenderSession);
    }
}
