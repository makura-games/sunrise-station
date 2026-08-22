using Content.Server.Inventory;
using Content.Server.Radio.EntitySystems;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Systems;
using Content.Server._Sunrise.Messenger;
using Content.Shared.Inventory;
using Content.Shared.Radio;
using Content.Shared.Roles;
using Content.Shared.Radio.Components;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.StationRecords;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Silicons.Borgs;

/// <summary>
/// Server-side logic for borg type switching. Handles more heavyweight and server-specific switching logic.
/// </summary>
public sealed partial class BorgSwitchableTypeSystem : SharedBorgSwitchableTypeSystem
{
    [Dependency] private BorgSystem _borgSystem = default!;
    [Dependency] private ServerInventorySystem _inventorySystem = default!;
    [Dependency] private StationRecordsSystem _record = default!;
    [Dependency] private MessengerServerSystem _messenger = default!;
    [Dependency] private StationSystem _station = default!;

    protected override void SelectBorgModule(Entity<BorgSwitchableTypeComponent> ent, ProtoId<BorgTypePrototype> borgType)
    {
        var prototype = Prototypes.Index(borgType);

        // Assign radio channels
        string[] radioChannels = [.. ent.Comp.InherentRadioChannels, .. prototype.RadioChannels];
        if (TryComp(ent, out IntrinsicRadioTransmitterComponent? transmitter))
            transmitter.Channels = [.. radioChannels];

        if (TryComp(ent, out ActiveRadioComponent? activeRadio))
            activeRadio.Channels = [.. radioChannels];

        // Borg transponder for the robotics console
        if (TryComp(ent, out BorgTransponderComponent? transponder))
        {
            _borgSystem.SetTransponderSprite(
                (ent.Owner, transponder),
                new SpriteSpecifier.Rsi(new ResPath("Mobs/Silicon/chassis.rsi"), prototype.SpriteBodyState));

            _borgSystem.SetTransponderName(
                (ent.Owner, transponder),
                Loc.GetString($"borg-type-{borgType}-transponder"));
        }

        // Configure modules
        if (TryComp(ent, out BorgChassisComponent? chassis))
        {
            var chassisEnt = (ent.Owner, chassis);
            _borgSystem.SetMaxModules(
                chassisEnt,
                prototype.ExtraModuleCount + prototype.DefaultModules.Length);

            _borgSystem.SetModuleWhitelist(chassisEnt, prototype.ModuleWhitelist);

            foreach (var module in prototype.DefaultModules)
            {
                var moduleEntity = Spawn(module);
                var borgModule = Comp<BorgModuleComponent>(moduleEntity);
                _borgSystem.SetBorgModuleDefault((moduleEntity, borgModule), true);
                _borgSystem.InsertModule(chassisEnt, moduleEntity);
            }
        }

        // Configure special components
        if (Prototypes.Resolve(ent.Comp.SelectedBorgType, out var previousPrototype))
        {
            if (previousPrototype.AddComponents is { } removeComponents)
                EntityManager.RemoveComponents(ent, removeComponents);
        }

        if (prototype.AddComponents is { } addComponents)
        {
            EntityManager.AddComponents(ent, addComponents);
        }

        // Configure inventory template (used for hat spacing)
        if (TryComp(ent, out InventoryComponent? inventory))
        {
            _inventorySystem.SetTemplateId((ent.Owner, inventory), prototype.InventoryTemplateId);
        }

        // Sunrise-Start
        var borgName = MetaData(ent.Owner).EntityName;
        if (Prototypes.TryIndex<JobPrototype>(prototype.Job, out var jobPrototype))
        {
            UpdateStationRecord(ent.Owner,
                jobPrototype.LocalizedName,
                jobPrototype);

            foreach (var channel in ent.Comp.NotifyChannels)
            {
                Report(ent.Owner,
                    channel,
                    Loc.GetString("borg-switch-type-notify",
                    ("name", borgName),
                    ("type", jobPrototype.LocalizedName)));
            }
        }
        // Sunrise-End

        base.SelectBorgModule(ent, borgType);
    }

    // Sunrise-Start
    public void UpdateStationRecord(EntityUid uid, string newJobTitle, JobPrototype? newJobProto)
    {
        if (!TryComp<StationRecordKeyStorageComponent>(uid, out var keyStorage)
            || keyStorage.Key is not { } key
            || !_record.TryGetRecord<GeneralStationRecord>(key, out var record))
        {
            return;
        }

        record.JobTitle = newJobTitle;

        if (newJobProto != null)
        {
            record.JobPrototype = newJobProto.ID;
            record.JobIcon = newJobProto.Icon;
        }

        _record.Synchronize(key);
    }

    private void Report(EntityUid source, string channelName, string message)
    {
        // var channel = Prototypes.Index<RadioChannelPrototype>(channelName);
        // _radioSystem.SendRadioMessage(source, message, channel, source);

        if (_messenger.GetServerEntity(_station.GetOwningStation(source)) is var (server, _) &&
            _messenger.GetGroupIdByRadioChannel(channelName) is { } groupId)
        {
            _messenger.SendSystemMessageToGroup(server, groupId, message);
        }
    }
    // Sunrise-End
}
