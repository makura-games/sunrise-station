using Content.Server._Sunrise.StationEvents.Events;
using Content.Server.GameTicking;
using Content.Server.RoundEnd;
using Content.Shared.AlertLevel;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.AlertLevel;

public sealed partial class SunriseAlertLevelSystem : EntitySystem
{
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;

    private static readonly ProtoId<AlertLevelPrototype> EpsilonAlertLevel = "Epsilon";
    private static readonly EntProtoId EpsilonBorgLawChanges = "EpsilonDeathSquadLawset";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SunriseAlertLevelChangedEvent>(OnAlertLevelChanged);
    }

    private void OnAlertLevelChanged(ref SunriseAlertLevelChangedEvent args)
    {
        if (!ProtoMan.Resolve(args.AlertLevel, out var alertLevel))
            return;

        if (alertLevel.ForceEndRound)
            _roundEnd.EndRound();

        if (args.AlertLevel != EpsilonAlertLevel)
            return;

        var rule = _gameTicker.AddGameRule(EpsilonBorgLawChanges);
        EntityManager.System<EpsilonDeathSquadLawsetRule>().StartEvent(rule, args.Station);
        _gameTicker.StartGameRule(rule);
    }
}
