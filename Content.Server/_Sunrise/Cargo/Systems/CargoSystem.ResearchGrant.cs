using Content.Shared.Cargo.Components;
using Content.Shared.Research.Components;
using Content.Shared.Research.Events;

// ReSharper disable once CheckNamespace
namespace Content.Server.Cargo.Systems;

public sealed partial class CargoSystem
{
    private void InitializeResearchGrant()
    {
        SubscribeLocalEvent<TechnologyDatabaseComponent, ResearchCargoGrantEvent>(OnResearchCargoGrant);
    }

    private void OnResearchCargoGrant(Entity<TechnologyDatabaseComponent> entity, ref ResearchCargoGrantEvent args)
    {
        if (_station.GetOwningStation(entity) is not { } station ||
            !TryComp<StationBankAccountComponent>(station, out var bank))
            return;

        UpdateBankAccount((station, bank), args.Amount, "Science");

        var message = Loc.GetString("research-messenger-grant-received", ("amount", args.Amount));
        if (_messenger.GetServerEntity(station) is var (server, _) &&
            _messenger.GetGroupIdByRadioChannel("Science") is { } groupId)
        {
            _messenger.SendSystemMessageToGroup(server, groupId, message);
        }
    }
}
