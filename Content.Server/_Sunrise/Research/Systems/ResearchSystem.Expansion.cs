using Content.Shared.Research.Components;
using Content.Shared.Research.Events;

// ReSharper disable once CheckNamespace
namespace Content.Server.Research.Systems;

public sealed partial class ResearchSystem
{
    private void InitializeResearchExpansion()
    {
        SubscribeLocalEvent<TechnologyDatabaseComponent, ResearchDisciplineUnlockEvent>(OnResearchDisciplineUnlock);
        SubscribeLocalEvent<TechnologyDatabaseComponent, ResearchDisciplineSelectedEvent>(OnResearchDisciplineSelected);
    }

    private void OnResearchDisciplineUnlock(
        Entity<TechnologyDatabaseComponent> entity,
        ref ResearchDisciplineUnlockEvent args)
    {
        AddResearchDisciplineSlot(entity, args.Amount);
    }

    private void OnResearchDisciplineSelected(
        Entity<TechnologyDatabaseComponent> entity,
        ref ResearchDisciplineSelectedEvent args)
    {
        var message = Loc.GetString("research-messenger-discipline-selected",
            ("discipline", Loc.GetString(ProtoMan.Index(args.Discipline).Name)));

        if (_messenger.GetServerEntity(_stationSystem.GetOwningStation(entity)) is var (server, _) &&
            _messenger.GetGroupIdByRadioChannel("Science") is { } groupId)
        {
            _messenger.SendSystemMessageToGroup(server, groupId, message);
        }
    }
}
