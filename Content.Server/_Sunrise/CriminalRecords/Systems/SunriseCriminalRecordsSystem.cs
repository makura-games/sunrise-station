using Content.Shared._Sunrise.CriminalRecords;
using Content.Shared._Sunrise.Laws;
using Content.Shared._Sunrise.CriminalRecords.Systems;
using Content.Shared.StationRecords;
using Content.Shared._Sunrise.CriminalRecords.Components;
using Content.Server._Sunrise.CriminalRecords.Components;
using Content.Server.StationRecords.Systems;
using Content.Server.Station.Systems;
using Content.Server._Sunrise.Laws.Systems;
using Content.Shared.Access.Systems;
using Content.Server.CriminalRecords.Systems;
using Content.Shared.CriminalRecords;
using Content.Shared.Security;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.CriminalRecords.Systems;

public sealed partial class SunriseCriminalRecordsSystem : SharedSunriseCriminalRecordsSystem
{
    [Dependency] private StationRecordsSystem _stationRecords = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private Robust.Shared.Timing.IGameTiming _timing = default!;
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private StationCorporateLawSystem _stationLaw = default!;
    [Dependency] private CriminalRecordsSystem _criminalRecords = default!;

    private const int MaxLaws = 20;
    private const int MaxCircumstances = 10;
    private const int MaxNotesLength = 2048;
    private const int MaxStatusReasonLength = 512;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<SunriseCriminalRecordsConsoleComponent>(SunriseCriminalRecordsConsoleKey.Key, subs =>
        {
            subs.Event<SunriseCriminalRecordsSelectRecordMessage>(OnSelectRecord);
            subs.Event<SunriseCriminalRecordsCreateCaseMessage>(OnCreateCase);
            subs.Event<SunriseCriminalRecordsUpdateCaseMessage>(OnUpdateCase);
            subs.Event<SunriseCriminalRecordsCloseCaseMessage>(OnCloseCase);
            subs.Event<SunriseCriminalRecordsSelectCaseMessage>(OnSelectCase);
            subs.Event<SunriseCriminalRecordsSetUIStateMessage>(OnSetUIState);
            subs.Event<SunriseCriminalRecordsChangeStatusMessage>(OnChangeStatus);
            subs.Event<SunriseCriminalRecordsReopenCaseMessage>(OnReopenCase);
        });

        SubscribeLocalEvent<SunriseCriminalRecordsConsoleComponent, BoundUIOpenedEvent>(OnOpened);

        SubscribeLocalEvent<SunriseCriminalRecordsConsoleComponent, RecordModifiedEvent>(OnRecordEvent);
        SubscribeLocalEvent<SunriseCriminalRecordsConsoleComponent, AfterGeneralRecordCreatedEvent>(OnRecordEvent);
        SubscribeLocalEvent<SunriseCriminalRecordsConsoleComponent, RecordRemovedEvent>(OnRecordEvent);
    }

    private void OnRecordEvent<T>(Entity<SunriseCriminalRecordsConsoleComponent> ent, ref T args)
    {
        UpdateUserInterface(ent.Owner, ent.Comp);
    }

    private void OnOpened(EntityUid uid, SunriseCriminalRecordsConsoleComponent component, BoundUIOpenedEvent args)
    {
        UpdateUserInterface(uid, component);
    }

    private void OnSelectRecord(EntityUid uid, SunriseCriminalRecordsConsoleComponent component, SunriseCriminalRecordsSelectRecordMessage msg)
    {
        if (!CheckAccess(uid, msg.Actor))
            return;

        if (_station.GetOwningStation(uid) is not { } station)
            return;

        if (msg.RecordId == null)
        {
            component.SelectedKey = null;
        }
        else
        {
            var key = new StationRecordKey(msg.RecordId.Value, station);
            if (!_stationRecords.TryGetRecord<GeneralStationRecord>(key, out _))
                return;

            component.SelectedKey = key;
        }

        component.CurrentUIState = SunriseCriminalRecordsUIState.List;
        component.SelectedCaseId = null;
        UpdateUserInterface(uid, component);
    }

    private void OnSelectCase(EntityUid uid, SunriseCriminalRecordsConsoleComponent component, SunriseCriminalRecordsSelectCaseMessage msg)
    {
        if (!CheckAccess(uid, msg.Actor))
            return;

        component.SelectedCaseId = msg.CaseId;
        component.CurrentUIState = SunriseCriminalRecordsUIState.Editor;
        UpdateUserInterface(uid, component);
    }

    private void OnCreateCase(EntityUid uid, SunriseCriminalRecordsConsoleComponent component, SunriseCriminalRecordsCreateCaseMessage msg)
    {
        if (!CheckAccess(uid, msg.Actor))
            return;

        if (component.SelectedKey == null)
            return;

        if (TryComp<StationCriminalRecordsComponent>(component.SelectedKey.Value.OriginStation, out var records))
        {
            var cases = records.Records.GetValueOrDefault(component.SelectedKey.Value.Id, new List<CriminalCase>());
            var nextId = records.NextCaseIds.GetValueOrDefault(component.SelectedKey.Value.Id, 1u);

            var @case = new CriminalCase(nextId, _timing.CurTime)
            {
                OriginStation = GetNetEntity(component.SelectedKey.Value.OriginStation)
            };
            cases.Add(@case);
            records.Records[component.SelectedKey.Value.Id] = cases;
            records.NextCaseIds[component.SelectedKey.Value.Id] = nextId + 1;

            component.SelectedCaseId = nextId;
        }

        component.CurrentUIState = SunriseCriminalRecordsUIState.Editor;
        UpdateUserInterface(uid, component);
    }

    private void OnSetUIState(EntityUid uid, SunriseCriminalRecordsConsoleComponent component, SunriseCriminalRecordsSetUIStateMessage msg)
    {
        if (!CheckAccess(uid, msg.Actor))
            return;

        component.CurrentUIState = msg.State;
        UpdateUserInterface(uid, component);
    }
    
    private void OnChangeStatus(EntityUid uid, SunriseCriminalRecordsConsoleComponent component, SunriseCriminalRecordsChangeStatusMessage msg)
    {
        if (!CheckAccess(uid, msg.Actor))
            return;

        if (component.SelectedKey == null)
            return;

        if (msg.Reason?.Length > MaxStatusReasonLength)
            return;

        var name = Comp<MetaDataComponent>(msg.Actor).EntityName;
        _criminalRecords.TryChangeStatus(component.SelectedKey.Value, msg.Status, msg.Reason, initiatorName: name);
        UpdateUserInterface(uid, component);
    }

    private void OnUpdateCase(EntityUid uid, SunriseCriminalRecordsConsoleComponent component, SunriseCriminalRecordsUpdateCaseMessage msg)
    {
        if (!CheckAccess(uid, msg.Actor))
            return;

        if (component.SelectedKey == null || msg.CaseId != component.SelectedCaseId)
            return;

        if (TryComp<StationCriminalRecordsComponent>(component.SelectedKey.Value.OriginStation, out var records))
        {
            if (!records.Records.TryGetValue(component.SelectedKey.Value.Id, out var cases))
                return;

            var @case = cases.Find(c => c.Id == msg.CaseId);

            if (@case != null && @case.Status == CriminalCaseStatus.Open)
            {
                // Validate limits
                if (msg.Laws.Count > MaxLaws || msg.Circumstances.Count > MaxCircumstances)
                    return;

                if (msg.Notes?.Length > MaxNotesLength)
                    return;

                // Validate laws and circumstances against lawset
                var station = component.SelectedKey.Value.OriginStation;
                var validatedLaws = new List<ProtoId<CorporateLawPrototype>>();
                foreach (var lawId in msg.Laws)
                {
                    if (!_stationLaw.IsLawInEffectiveLawset(lawId, station))
                        return; // Reject message if any ID is invalid
                    validatedLaws.Add(lawId);
                }

                var validatedCircs = new List<ProtoId<CorporateLawPrototype>>();
                foreach (var circId in msg.Circumstances)
                {
                    if (!_stationLaw.IsCircumstanceInEffectiveLawset(circId, station))
                        return; // Reject message if any ID is invalid
                    validatedCircs.Add(circId);
                }

                @case.Laws = validatedLaws;
                @case.Circumstances = validatedCircs;
                @case.Notes = msg.Notes;
                @case.CalculatedSentence = CalculateSentence(@case, cases);
            }
        }

        UpdateUserInterface(uid, component);
    }

    private void OnCloseCase(EntityUid uid, SunriseCriminalRecordsConsoleComponent component, SunriseCriminalRecordsCloseCaseMessage msg)
    {
        if (!CheckAccess(uid, msg.Actor))
            return;

        if (component.SelectedKey == null || msg.CaseId != component.SelectedCaseId)
            return;

        if (TryComp<StationCriminalRecordsComponent>(component.SelectedKey.Value.OriginStation, out var records))
        {
            if (records.Records.TryGetValue(component.SelectedKey.Value.Id, out var cases))
            {
                var @case = cases.Find(c => c.Id == msg.CaseId);

                if (@case != null && @case.Status == CriminalCaseStatus.Open)
                {
                    @case.CalculatedSentence = CalculateSentence(@case, cases);
                    @case.Status = @case.IsWarning ? CriminalCaseStatus.Finished : CriminalCaseStatus.Closed;
                }
            }
        }

        component.CurrentUIState = SunriseCriminalRecordsUIState.List;
        component.SelectedCaseId = null;
        UpdateUserInterface(uid, component);
    }

    private void OnReopenCase(EntityUid uid, SunriseCriminalRecordsConsoleComponent component, SunriseCriminalRecordsReopenCaseMessage msg)
    {
        if (TryReopenCase(uid, component, msg.Actor, msg.CaseId))
            UpdateUserInterface(uid, component);
    }

    public bool TryReopenCase(EntityUid uid, SunriseCriminalRecordsConsoleComponent component, EntityUid actor, uint caseId)
    {
        if (!CanReopenCase(uid, component, actor, caseId, out var @case, out var cases))
            return false;

        @case.Status = CriminalCaseStatus.Open;
        @case.CalculatedSentence = CalculateSentence(@case, cases);
        return true;
    }

    public bool CanReopenCase(EntityUid uid, SunriseCriminalRecordsConsoleComponent component, EntityUid actor, uint caseId, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out CriminalCase? @case, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out List<CriminalCase>? cases)
    {
        @case = null;
        cases = null;

        if (!CheckAccess(uid, actor))
            return false;

        if (component.SelectedKey == null)
            return false;

        if (!TryComp<StationCriminalRecordsComponent>(component.SelectedKey.Value.OriginStation, out var records))
            return false;

        if (!records.Records.TryGetValue(component.SelectedKey.Value.Id, out cases))
            return false;

        @case = cases.Find(c => c.Id == caseId);
        if (@case == null)
            return false;

        // Allow reopening if closed (waiting for incarceration) or finished (but only if it's a warning)
        return @case.Status == CriminalCaseStatus.Closed || (@case.Status == CriminalCaseStatus.Finished && @case.IsWarning);
    }

    private bool CheckAccess(EntityUid console, EntityUid? user)
    {
        if (user == null)
            return false;

        return _accessReader.IsAllowed(user.Value, console);
    }


    private void UpdateUserInterface(EntityUid uid, SunriseCriminalRecordsConsoleComponent component)
    {
        var station = _station.GetOwningStation(uid);
        if (!TryComp<StationRecordsComponent>(station, out var stationRecordsComp))
            return;

        var listingRecords = new List<SunriseCriminalRecordListing>();
        foreach (var (id, _) in _stationRecords.BuildListing((station.Value, stationRecordsComp), null))
        {
            var key = new StationRecordKey(id, station.Value);
            if (_stationRecords.TryGetRecord<GeneralStationRecord>(key, out var general))
            {
                listingRecords.Add(new SunriseCriminalRecordListing(
                    id, 
                    general.Name, 
                    general.DNA, 
                    general.Fingerprint, 
                    general.Species, 
                    general.Gender.ToString(),
                    general.JobTitle));
            }
        }

        string? selectedName = null;
        string? jobTitle = null;
        string? jobIcon = null;
        int? age = null;
        string? gender = null;
        string? species = null;
        string? fingerprints = null;
        string? dna = null;
        SecurityStatus status = SecurityStatus.None;
        string? statusReason = null;
        List<CriminalCase> cases = new();

        if (component.SelectedKey != null)
        {
            if (_stationRecords.TryGetRecord<GeneralStationRecord>(component.SelectedKey.Value, out var general))
            {
                selectedName = general.Name;
                jobTitle = general.JobTitle;
                jobIcon = general.JobIcon;
                age = general.Age;
                gender = general.Gender.ToString();
                species = general.Species;
                fingerprints = general.Fingerprint;
                dna = general.DNA;
            }

            if (_stationRecords.TryGetRecord<CriminalRecord>(component.SelectedKey.Value, out var criminal))
            {
                status = criminal.Status;
                statusReason = criminal.Reason;
            }

            if (TryComp<StationCriminalRecordsComponent>(station.Value, out var criminalRecords))
            {
                if (criminalRecords.Records.TryGetValue(component.SelectedKey.Value.Id, out var personCases))
                {
                    cases = personCases;
                }
            }
        }

        var state = new SunriseCriminalRecordsConsoleState(
            listingRecords,
            selectedName,
            cases,
            component.SelectedKey?.Id,
            component.SelectedCaseId,
            component.CurrentUIState,
            jobTitle,
            jobIcon,
            age,
            gender,
            species,
            fingerprints,
            dna,
            status,
            statusReason);
        _ui.SetUiState(uid, SunriseCriminalRecordsConsoleKey.Key, state);
    }

}
