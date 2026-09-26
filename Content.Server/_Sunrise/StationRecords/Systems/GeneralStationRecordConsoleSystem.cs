using Content.Server.Hands.Systems;
using Content.Server.Popups;
using Content.Server.Roles.Jobs;
using Content.Server._Sunrise.Messenger;
using Content.Shared._Sunrise.StationRecords;
using Content.Shared.Access.Systems;
using Content.Shared.Emag.Systems;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Paper;
using Content.Shared.Roles;
using Content.Shared.StationRecords;
using Content.Shared.StationRecords.Components;
using Content.Shared.StationRecords.Systems;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.StationRecords;

public sealed partial class GeneralStationRecordConsoleSystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private HandsSystem _hands = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private JobSystem _job = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private MessengerServerSystem _messenger = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GeneralStationRecordConsoleComponent, GotEmaggedEvent>(OnEmagged);

        Subs.BuiEvents<GeneralStationRecordConsoleComponent>(GeneralStationRecordConsoleKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<SaveStationRecord>(OnSave);
            subs.Event<PrintStationRecord>(OnPrint);
        });
    }

    protected override bool CanDeleteRecord(
        Entity<GeneralStationRecordConsoleComponent> ent,
        DeleteStationRecord args)
    {
        if (!ent.Comp.CanDeleteEntries || !HasAccess(ent, args.Actor))
            return false;

        var owning = StationSys.GetOwningStation(ent);
        if (owning == null ||
            !StationRecordsSys.TryGetRecord<GeneralStationRecord>(
                new StationRecordKey(args.Id, owning.Value),
                out var record))
        {
            return false;
        }

        var message = Loc.GetString("station-record-deleted", ("name", record.Name));
        var popup = Loc.GetString("station-record-deleted-successfully");
        DoFeedback(ent, message, popup);
        return true;
    }

    private void OnSave(Entity<GeneralStationRecordConsoleComponent> ent, ref SaveStationRecord args)
    {
        var owning = StationSys.GetOwningStation(ent);
        if (owning == null || !HasAccess(ent, args.Actor))
        {
            _audio.PlayPvs(ent.Comp.FailedSound, ent);
            return;
        }

        var key = new StationRecordKey(args.Id, owning.Value);
        if (!StationRecordsSys.TryGetRecord<GeneralStationRecord>(key, out _))
        {
            _audio.PlayPvs(ent.Comp.FailedSound, ent);
            return;
        }

        var record = GeneralStationRecord.SanitizeRecord(args.Record, in ProtoMan);
        StationRecordsSys.AddRecordEntry(key, record);
        StationRecordsSys.Synchronize(key);

        var message = Loc.GetString("station-record-updated", ("name", record.Name));
        var popup = Loc.GetString("station-record-updated-successfully");
        DoFeedback(ent, message, popup);
    }

    private void OnEmagged(Entity<GeneralStationRecordConsoleComponent> ent, ref GotEmaggedEvent args)
    {
        if (args.Handled ||
            ent.Comp.CanRedactSensitiveData &&
            ent.Comp.CanDeleteEntries &&
            ent.Comp.Silent &&
            ent.Comp.SkipAccessCheck)
        {
            return;
        }

        ent.Comp.CanDeleteEntries = true;
        ent.Comp.CanRedactSensitiveData = true;
        ent.Comp.Silent = true;
        ent.Comp.SkipAccessCheck = true;
        Dirty(ent);
        args.Handled = true;
    }

    private void OnPrint(Entity<GeneralStationRecordConsoleComponent> ent, ref PrintStationRecord args)
    {
        var user = args.Actor;
        if (_timing.CurTime < ent.Comp.NextPrintTime)
        {
            _popup.PopupEntity(Loc.GetString("forensic-scanner-printer-not-ready"), ent, user);
            _audio.PlayPvs(ent.Comp.FailedSound, ent);
            return;
        }

        var owning = StationSys.GetOwningStation(ent);
        if (owning == null ||
            !StationRecordsSys.TryGetRecord<GeneralStationRecord>(
                new StationRecordKey(args.Id, owning.Value),
                out var record))
        {
            _audio.PlayPvs(ent.Comp.FailedSound, ent);
            return;
        }

        var printed = Spawn(ent.Comp.Paper, Transform(ent).Coordinates);
        _hands.PickupOrDrop(user, printed, checkActionBlocker: false);

        if (!TryComp<PaperComponent>(printed, out var paperComp))
        {
            _audio.PlayPvs(ent.Comp.FailedSound, ent);
            return;
        }

        var documentName = Loc.GetString("printed-station-records-document-name", ("name", record.Name));
        _metaData.SetEntityName(printed, documentName);

        var text = Loc.GetString(
            "printed-station-records-content",
            ("name", record.Name),
            ("job", GetJobName(record.JobPrototype)),
            ("department", GetDepartmentName(record.JobPrototype)),
            ("age", record.Age),
            ("gender", GetGenderName(record.Gender)),
            ("species", GetSpeciesName(record.Species)),
            ("dna", record.DNA ?? Loc.GetString("printed-station-records-unrecognized")),
            ("fingerprint", record.Fingerprint ?? Loc.GetString("printed-station-records-unrecognized")),
            ("personality", GetPersonality(record.Personality)));

        _paper.SetContent((printed, paperComp), text);
        _audio.PlayPvs(ent.Comp.SoundPrint, ent,
            AudioParams.Default
                .WithVariation(0.25f)
                .WithVolume(4f)
                .WithRolloffFactor(2.8f)
                .WithMaxDistance(4.5f));

        ent.Comp.NextPrintTime = _timing.CurTime + ent.Comp.PrintCooldown;
    }

    private void DoFeedback(Entity<GeneralStationRecordConsoleComponent> ent, string message, string popup)
    {
        _popup.PopupEntity(popup, ent);

        if (ent.Comp.Silent)
            return;

        var server = _messenger.GetServerEntity(StationSys.GetOwningStation(ent));
        foreach (var channel in ent.Comp.AnnouncementChannels)
        {
            if (_messenger.GetGroupIdByRadioChannel(channel) is { } groupId && server != null)
                _messenger.SendSystemMessageToGroup(server.Value.Item1, groupId, message);
        }

        _audio.PlayPvs(ent.Comp.SuccessfulSound, ent);
    }

    private void OnOpened(Entity<GeneralStationRecordConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        ent.Comp.HasAccess = HasAccess(ent, args.Actor);
        Dirty(ent);
    }

    private bool HasAccess(Entity<GeneralStationRecordConsoleComponent> ent, EntityUid actor)
    {
        return ent.Comp.SkipAccessCheck || _access.IsAllowed(actor, ent);
    }

    private string GetJobName(ProtoId<JobPrototype> job)
    {
        return ProtoMan.TryIndex(job, out var jobPrototype)
            ? jobPrototype.LocalizedName
            : Loc.GetString("printed-station-records-unrecognized");
    }

    private string GetDepartmentName(ProtoId<JobPrototype> job)
    {
        return _job.TryGetDepartment(job, out var department)
            ? Loc.GetString(department.Name)
            : Loc.GetString("printed-station-records-unrecognized");
    }

    private string GetGenderName(Gender gender)
    {
        return Loc.GetString("station-records-gender", ("gender", gender.ToString()));
    }

    private string GetSpeciesName(ProtoId<SpeciesPrototype> species)
    {
        return ProtoMan.TryIndex(species, out var speciesPrototype)
            ? Loc.GetString(speciesPrototype.Name)
            : Loc.GetString("printed-station-records-unrecognized");
    }

    private string GetPersonality(string personality)
    {
        return string.IsNullOrEmpty(personality)
            ? Loc.GetString("printed-station-records-unrecognized")
            : personality;
    }
}
