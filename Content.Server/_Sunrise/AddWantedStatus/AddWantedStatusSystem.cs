using Content.Server.Actions;
using Content.Server.CriminalRecords.Systems;
using Content.Server.Radio.EntitySystems;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Systems;
using Content.Server._Sunrise.Messenger;
using Content.Shared._Sunrise.AddWantedStatus;
using Content.Shared.Actions;
using Content.Shared.Clothing.Components;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Security;
using Content.Shared.StationRecords;

namespace Content.Server._Sunrise.AddWantedStatus;

public sealed partial class AddWantedStatusSystem : EntitySystem
{
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private CriminalRecordsSystem _criminalRecords = default!;
    [Dependency] private IdentitySystem _identity = default!;
    [Dependency] private StationRecordsSystem _records = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private MessengerServerSystem _messenger = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AddWantedStatusComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AddWantedStatusComponent, AddWantedEvent>(OnAddWanted);
        SubscribeLocalEvent<AddWantedStatusComponent, GetItemActionsEvent>(OnGetItemActions);
    }

    private void OnMapInit(Entity<AddWantedStatusComponent> ent, ref MapInitEvent args)
    {
        var (uid, comp) = ent;
        if (string.IsNullOrEmpty(comp.Action))
            return;

        _actions.AddAction(uid, ref comp.ActionEntity, comp.Action);
    }

    private void OnAddWanted(Entity<AddWantedStatusComponent> ent, ref AddWantedEvent args)
    {
        var target = args.Target;
        var performer = args.Performer;
        if (!HasComp<HumanoidProfileComponent>(target))
            return;

        if (_station.GetOwningStation(performer) is not { } station)
            return;

        var targetName = Identity.Name(target, EntityManager);

        foreach (var (key, record) in _records.GetRecordsOfType<GeneralStationRecord>(station))
        {
            if (!string.Equals(record.Name?.Trim(), targetName?.Trim(), StringComparison.OrdinalIgnoreCase))
                continue;

            var recordKey = new StationRecordKey(key, station);
            var reason = Loc.GetString("criminal-records-reason-visor");

            if (_criminalRecords.TryChangeStatus(recordKey, SecurityStatus.Wanted, reason))
            {
                SendRadioMessage(ent, reason, performer, record);
                break;
            }
        }
    }

    private void OnGetItemActions(Entity<AddWantedStatusComponent> ent, ref GetItemActionsEvent args)
    {
        if (!TryComp<ClothingComponent>(ent.Owner, out var comp))
            return;

        if (comp.Slots != args.SlotFlags)
            return;

        args.AddAction(ent.Comp.ActionEntity);
    }


    private void SendRadioMessage(EntityUid sender, string? reason, EntityUid officerUid, GeneralStationRecord record)
    {
        var wantedName = record.Name;
        var wantedJobTitle = record.JobTitle ?? Loc.GetString("job-name-unknown");
        var officer = Loc.GetString("criminal-records-console-unknown-officer");

        // Officer
        officer = _identity.GetIdentityShortInfo(officerUid) ?? officer;

        // Reason
        if (string.IsNullOrWhiteSpace(reason))
            reason = Loc.GetString("wanted-list-unknown-reason-label");

        var message = Loc.GetString("criminal-records-console-wanted", [("name", wantedName), ("officer", officer), ("reason", reason), ("job", wantedJobTitle)]);

        if (_messenger.GetServerEntity(_station.GetOwningStation(sender)) is var (server, _) &&
            _messenger.GetGroupIdByRadioChannel("Security") is { } groupId)
        {
            _messenger.SendSystemMessageToGroup(server, groupId, message);
        }
    }
}
