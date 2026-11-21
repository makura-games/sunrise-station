using Content.Server._Sunrise.Disease.Systems;
using Content.Server._Sunrise.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared._Sunrise.Disease.Components;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.StationEvents.Events;

/// <summary>
/// Авария в вирусологии: культура из сосуда вырывается наружу и становится вспышкой.
/// </summary>
public sealed partial class DiseaseVesselBreachRule : StationEventSystem<DiseaseVesselBreachRuleComponent>
{
    [Dependency] private DiseaseOutbreakRule _outbreak = default!;
    [Dependency] private DiseaseResearchVesselSystem _vessel = default!;

    protected override void Started(
        EntityUid uid,
        DiseaseVesselBreachRuleComponent component,
        GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!TryGetRandomStation(out var station))
            return;

        var candidates = new List<Entity<DiseaseResearchVesselComponent>>();
        var query = EntityQueryEnumerator<DiseaseResearchVesselComponent>();
        while (query.MoveNext(out var vessel, out var comp))
        {
            if (StationSystem.GetOwningStation(vessel) == station && _vessel.CanBreach((vessel, comp)))
                candidates.Add((vessel, comp));
        }

        // Без сосуда с заразной культурой событие проходит молча.
        if (candidates.Count == 0 || !_vessel.TryBreach(RobustRandom.Pick(candidates), out var disease))
            return;

        var count = _outbreak.InfectCrew(station.Value, disease, component.MinInfected, component.MaxInfected);

        var players = Filter.Empty().AddWhere(GameTicker.UserHasJoinedGame);
        ChatSystem.DispatchFilteredAnnouncement(players,
            Loc.GetString(component.Announcement),
            playDefault: false,
            announcementSound: component.AnnouncementSound,
            colorOverride: component.AnnouncementColor);

        Sawmill.Info($"Disease vessel breach: {ToPrettyString(disease)} infected {count} crew members");
    }
}
