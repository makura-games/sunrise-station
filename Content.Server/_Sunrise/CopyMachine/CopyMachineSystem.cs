using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Content.Server.Materials;
using Content.Server._Sunrise.Documents;
using Content.Shared._Sunrise.CopyMachine;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.Buckle.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Materials;
using Content.Shared.Paper;
using Content.Shared.Labels.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.CopyMachine;

public sealed partial class CopyMachineSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private UserInterfaceSystem _userInterface = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private MaterialStorageSystem _materialStorage = default!;
    [Dependency] private IResourceManager _resourceManager = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private IConfigurationManager _configManager = default!;
    [Dependency] private EmagSystem _emag = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private LabelSystem _label = default!;
    [Dependency] private DocumentFormatSystem _documentFormat = default!;

    private readonly Dictionary<string, string> _documentContentByTemplateId = new();

    private static readonly Regex DocumentTagRegex =
        new("<Document>(.*?)</Document>", RegexOptions.Singleline | RegexOptions.Compiled);

    private readonly Queue<EntityUid> _pendingUIUpdateQueue = new();
    private readonly HashSet<EntityUid> _pendingUIUpdateSet = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CopyMachineComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<CopyMachineComponent, CopyMachinePrintMessage>(OnPrintRequested);
        SubscribeLocalEvent<CopyMachineComponent, CopyMachineCopyMessage>(OnCopyRequested);
        SubscribeLocalEvent<CopyMachineComponent, CopyMachineCancelJobMessage>(OnCancelJobRequested);
        SubscribeLocalEvent<CopyMachineComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CopyMachineComponent, SolutionChangedEvent>(OnSolutionChanged);
        SubscribeLocalEvent<CopyMachineComponent, EntInsertedIntoContainerMessage>(OnEntityInsertedIntoCopySlot);
        SubscribeLocalEvent<CopyMachineComponent, EntRemovedFromContainerMessage>(OnEntityRemovedFromCopySlot);
        SubscribeLocalEvent<CopyMachineComponent, MaterialAmountChangedEvent>(OnMaterialAmountChanged);
        SubscribeLocalEvent<CopyMachineComponent, StrappedEvent>(OnEntityStrapped);
        SubscribeLocalEvent<CopyMachineComponent, UnstrappedEvent>(OnEntityUnstrapped);
        SubscribeLocalEvent<CopyMachineComponent, GotEmaggedEvent>(OnEmagged);
        SubscribeLocalEvent<CopyMachineComponent, ComponentShutdown>(OnShutdown);

        _configManager.OnValueChanged(SunriseCCVars.DocumentTemplatePool, _ =>
        {
            RebuildDocumentTemplateContentCache();
            RefreshAllCopyMachineTemplates();
        }, false);

        RebuildDocumentTemplateContentCache();
    }

    private void OnCancelJobRequested(Entity<CopyMachineComponent> ent, ref CopyMachineCancelJobMessage args)
    {
        if (args.Index < 0 || args.Index >= ent.Comp.JobQueue.Count)
            return;

        var removed = false;
        var count = ent.Comp.JobQueue.Count;
        for (var i = 0; i < count; i++)
        {
            var job = ent.Comp.JobQueue.Dequeue();
            if (i == args.Index)
            {
                removed = true;
                continue;
            }

            ent.Comp.JobQueue.Enqueue(job);
        }

        if (removed)
        {
            RefundInkAndPaper(ent);
            QueueUIUpdate(ent);
        }
    }

    private void RefundInkAndPaper(Entity<CopyMachineComponent> ent)
    {
        _materialStorage.TryChangeMaterialAmount(ent, ent.Comp.PaperMaterial, ent.Comp.PaperCost);

        if (!_solutionContainer.TryGetSolution(ent.Owner, ent.Comp.Solution, out var solutionEntity, out _))
            return;

        var inkReagentId = new ReagentId(ent.Comp.IncReagentProto, null);
        _solutionContainer.TryAddReagent(solutionEntity.Value, inkReagentId, ent.Comp.IncCost, out _);
    }

    private bool TryGetConfiguredTemplatePool([NotNullWhen(true)] out DocTemplatePoolPrototype? templatePoolPrototype)
    {
        var templatePoolId = _configManager.GetCVar(SunriseCCVars.DocumentTemplatePool);
        return _prototypeManager.TryIndex(templatePoolId, out templatePoolPrototype);
    }

    private void RebuildDocumentTemplateContentCache()
    {
        _documentContentByTemplateId.Clear();

        if (!TryGetConfiguredTemplatePool(out var templatePoolPrototype))
            return;

        foreach (var templateId in templatePoolPrototype.Templates)
        {
            if (!_prototypeManager.TryIndex(templateId, out var templatePrototype))
                continue;

            using var file = _resourceManager.ContentFileReadText(templatePrototype.Content);
            var fileText = file.ReadToEnd();
            var match = DocumentTagRegex.Match(fileText);
            var content = match.Success ? match.Groups[1].Value.Trim() : fileText.Trim();

            _documentContentByTemplateId[templatePrototype.ID] = content;
        }
    }

    // Мгновенная смена шаблонов в принтерах при смене CVar-а
    private void RefreshAllCopyMachineTemplates()
    {
        var copyMachineEnumerator = EntityQueryEnumerator<CopyMachineComponent>();
        while (copyMachineEnumerator.MoveNext(out var copyMachineUid, out var copyMachineComponent))
        {
            UpdateAvailableTemplates((copyMachineUid, copyMachineComponent));
        }
    }

    private void UpdateAvailableTemplates(Entity<CopyMachineComponent> ent)
    {
        ent.Comp.Templates.Clear();

        var isEmagged = HasComp<EmaggedComponent>(ent);

        if (!TryGetConfiguredTemplatePool(out var templatePoolPrototype))
            return;

        foreach (var templateId in templatePoolPrototype.Templates)
        {
            if (!_prototypeManager.TryIndex(templateId, out var templatePrototype))
                continue;

            if (!IsTemplateCategoryAllowed(ent, templatePrototype.Category))
                continue;

            if (templatePrototype.IsPublic || isEmagged)
                ent.Comp.Templates.Add(templatePrototype.ID);
        }

        QueueUIUpdate(ent);
    }

    private bool IsTemplateCategoryAllowed(Entity<CopyMachineComponent> ent, ProtoId<DocTemplateCategoryPrototype> categoryId)
    {
        if (ent.Comp.TemplateCategoryGroupId is not { } groupId)
            return true;

        return _prototypeManager.TryIndex(groupId, out DocTemplateCategoryGroupPrototype? group) &&
            group.Categories.Contains(categoryId);
    }

    private void OnShutdown(Entity<CopyMachineComponent> ent, ref ComponentShutdown args)
    {
        _pendingUIUpdateSet.Remove(ent);
    }

    private void OnUiOpened(Entity<CopyMachineComponent> ent, ref BoundUIOpenedEvent args) => UpdateUserInterface(ent);
    private void OnSolutionChanged(Entity<CopyMachineComponent> ent, ref SolutionChangedEvent args) => QueueUIUpdate(ent);
    private void OnEntityRemovedFromCopySlot(Entity<CopyMachineComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != CopyMachineComponent.CopySlotId)
            return;

        QueueUIUpdate(ent);
    }

    private void OnMaterialAmountChanged(Entity<CopyMachineComponent> ent, ref MaterialAmountChangedEvent args) => QueueUIUpdate(ent);

    private void OnEntityInsertedIntoCopySlot(Entity<CopyMachineComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != CopyMachineComponent.CopySlotId)
            return;

        QueueUIUpdate(ent);
    }

    private void QueueUIUpdate(EntityUid copyMachineUid)
    {
        if (!_userInterface.IsUiOpen(copyMachineUid, CopyMachineUiKey.Key))
            return;

        if (_pendingUIUpdateSet.Add(copyMachineUid))
            _pendingUIUpdateQueue.Enqueue(copyMachineUid);
    }

    private void QueueUIUpdate(Entity<CopyMachineComponent> ent) => QueueUIUpdate(ent.Owner);

    private void FlushUIUpdates()
    {
        while (_pendingUIUpdateQueue.TryDequeue(out var copyMachineUid))
        {
            _pendingUIUpdateSet.Remove(copyMachineUid);

            if (!_userInterface.IsUiOpen(copyMachineUid, CopyMachineUiKey.Key))
                continue;

            if (!TryComp(copyMachineUid, out CopyMachineComponent? copyMachineComponent))
                continue;

            UpdateUserInterface(new Entity<CopyMachineComponent>(copyMachineUid, copyMachineComponent));
        }
    }

    private void OnPrintRequested(Entity<CopyMachineComponent> ent, ref CopyMachinePrintMessage message)
    {
        TryQueueJob(ent, CopyMachineJobType.Print, message.TemplateId);
    }

    private void OnCopyRequested(Entity<CopyMachineComponent> ent, ref CopyMachineCopyMessage message)
    {
        TryQueueJob(ent, CopyMachineJobType.Copy, null);
    }

    private void OnMapInit(Entity<CopyMachineComponent> ent, ref MapInitEvent args)
    {
        _itemSlots.AddItemSlot(ent, CopyMachineComponent.CopySlotId, ent.Comp.CopySlot);
        UpdateRunningAppearance(ent, false);

        UpdateAvailableTemplates(ent);

        QueueUIUpdate(ent);
    }

    private void OnEmagged(Entity<CopyMachineComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        args.Handled = true;

        ent.Comp.Templates.Clear();

        if (!TryGetConfiguredTemplatePool(out var templatePoolPrototype))
            return;

        foreach (var templateId in templatePoolPrototype.Templates)
        {
            if (!_prototypeManager.TryIndex(templateId, out var templatePrototype))
                continue;

            if (!IsTemplateCategoryAllowed(ent, templatePrototype.Category))
                continue;

            ent.Comp.Templates.Add(templatePrototype.ID);
        }

        Dirty(ent);
        QueueUIUpdate(ent);
    }

    private void TryQueueJob(Entity<CopyMachineComponent> ent, CopyMachineJobType jobType, string? templateId)
    {
        if (ent.Comp.JobQueue.Count >= ent.Comp.MaxQueueSize)
            return;

        if (jobType == CopyMachineJobType.Print &&
            (templateId == null || !CanQueuePrintTemplate(ent, templateId)))
        {
            return;
        }

        if (!TryConsumeInkAndPaper(ent))
            return;

        ent.Comp.JobQueue.Enqueue((jobType, templateId));

        QueueUIUpdate(ent);
    }

    private string GetJobDisplayTitle(CopyMachineJobType jobType, string? templateId)
    {
        if (jobType == CopyMachineJobType.Print &&
            templateId != null &&
            _prototypeManager.TryIndex<DocTemplatePrototype>(templateId, out var templatePrototype))
        {
            return Loc.GetString(templatePrototype.Name);
        }

        return templateId ?? Loc.GetString("copy-machine-default-document-title");
    }

    private void StartQueuedJob(Entity<CopyMachineComponent> ent, CopyMachineJobType jobType, string? templateId, TimeSpan currentTime)
    {
        ent.Comp.IsProcessing = true;
        ent.Comp.NextPrintTime = currentTime + TimeSpan.FromSeconds(ent.Comp.JobDuration);
        UpdateRunningAppearance(ent, true);

        ent.Comp.CurrentJobView = new CopyMachineJobView(GetJobDisplayTitle(jobType, templateId), jobType, templateId);

        QueueUIUpdate(ent);
        _audioSystem.PlayPvs(ent.Comp.PrintSound, ent);
    }

    private void CompleteCurrentJob(Entity<CopyMachineComponent> ent)
    {
        var jobView = ent.Comp.CurrentJobView;
        if (jobView != null)
        {
            var jobType = jobView.Type;
            var templateId = jobView.TemplateId;
            _ = jobType switch
            {
                CopyMachineJobType.Print when templateId != null => TryPrintFromTemplate(ent, templateId),
                CopyMachineJobType.Copy => TryCopyFromSlotOrButtScan(ent),
                _ => false
            };
        }

        ent.Comp.IsProcessing = false;
        ent.Comp.CurrentJobView = null;
        ent.Comp.NextPrintTime = TimeSpan.Zero;
        UpdateRunningAppearance(ent, false);

        QueueUIUpdate(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var copyMachineEnumerator = EntityQueryEnumerator<CopyMachineComponent>();
        var currentTime = _gameTiming.CurTime;

        while (copyMachineEnumerator.MoveNext(out var copyMachineUid, out var copyMachineComponent))
        {
            var ent = new Entity<CopyMachineComponent>(copyMachineUid, copyMachineComponent);

            if (!ent.Comp.IsProcessing && ent.Comp.JobQueue.Count > 0)
            {
                var (jobType, templateId) = ent.Comp.JobQueue.Dequeue();
                StartQueuedJob(ent, jobType, templateId, currentTime);
            }

            if (ent.Comp.IsProcessing && currentTime >= ent.Comp.NextPrintTime)
                CompleteCurrentJob(ent);
        }

        FlushUIUpdates();
    }

    private bool TryConsumeInkAndPaper(Entity<CopyMachineComponent> ent)
    {
        if (!_solutionContainer.TryGetSolution(ent.Owner, ent.Comp.Solution, out var solutionEntity, out var solution))
            return false;

        var inkReagentId = new ReagentId(ent.Comp.IncReagentProto, null);

        if (!solution.TryGetReagentQuantity(inkReagentId, out var availableInkVolume) || availableInkVolume < ent.Comp.IncCost)
            return false;

        if (!_materialStorage.TryChangeMaterialAmount(ent, ent.Comp.PaperMaterial, -ent.Comp.PaperCost))
            return false;

        _solutionContainer.RemoveReagent(solutionEntity.Value, inkReagentId, ent.Comp.IncCost);

        return true;
    }

    private bool CanQueuePrintTemplate(Entity<CopyMachineComponent> ent, string templateId)
    {
        if (!HasAvailableTemplate(ent, templateId))
            return false;

        if (!_prototypeManager.TryIndex(templateId, out DocTemplatePrototype? templatePrototype))
            return false;

        if (!IsTemplateCategoryAllowed(ent, templatePrototype.Category))
            return false;

        if (!templatePrototype.IsPublic && !HasComp<EmaggedComponent>(ent))
            return false;

        return _documentContentByTemplateId.ContainsKey(templateId);
    }

    private bool HasAvailableTemplate(Entity<CopyMachineComponent> ent, string templateId)
    {
        foreach (var availableTemplateId in ent.Comp.Templates)
        {
            if (availableTemplateId == templateId)
                return true;
        }

        return false;
    }

    private bool TryPrintFromTemplate(Entity<CopyMachineComponent> ent, string templateId)
    {
        if (!CanQueuePrintTemplate(ent, templateId))
            return false;

        if (!_documentContentByTemplateId.TryGetValue(templateId, out var templateContent) ||
            !_prototypeManager.TryIndex<DocTemplatePrototype>(templateId, out var templatePrototype))
            return false;

        var paperEntity = Spawn(ent.Comp.PaperProtoId, Transform(ent).Coordinates);

        if (!TryComp<PaperComponent>(paperEntity, out var paperComponent))
        {
            Log.Error($"{ToPrettyString(ent):entity} spawned '{ent.Comp.PaperProtoId}' without a {nameof(PaperComponent)}.");
            Del(paperEntity);
            return false;
        }

        // ЕДИНАЯ система форматирования документов
        templateContent = _documentFormat.Format(templateContent, ent);

        _paper.SetContent((paperEntity, paperComponent), templateContent);

        if (templatePrototype.Header != null)
            _paper.SetImageContent((paperEntity, paperComponent), templatePrototype.Header);

        return true;
    }

    /// <summary>
    /// Updates the copy machine UI with the current paper, ink, and queue state.
    /// </summary>
    public void UpdateUserInterface(Entity<CopyMachineComponent> ent)
    {
        if (!_solutionContainer.TryGetSolution(ent.Owner, ent.Comp.Solution, out _, out var solution))
            return;

        var inkReagentId = new ReagentId(ent.Comp.IncReagentProto, null);
        float availableInkVolume = solution.TryGetReagentQuantity(inkReagentId, out var inkQuantity) ? inkQuantity.Value : 0;
        var availablePaperAmount = _materialStorage.GetMaterialAmount(ent, ent.Comp.PaperMaterial);

        var availableTemplateIds = new List<string>(ent.Comp.Templates.Count);
        foreach (var template in ent.Comp.Templates)
        {
            availableTemplateIds.Add(template.ToString());
        }

        var queuedJobs = new List<CopyMachineJobView>(ent.Comp.JobQueue.Count);
        foreach (var job in ent.Comp.JobQueue)
        {
            queuedJobs.Add(new CopyMachineJobView(GetJobDisplayTitle(job.Type, job.TemplateId), job.Type, job.TemplateId));
        }

        var state = new CopyMachineBoundUserInterfaceState(
            paperCount: availablePaperAmount / 100,
            inkAmount: availableInkVolume / 100,
            templates: availableTemplateIds,
            canCopy: CanCopy(ent),
            currentJob: ent.Comp.CurrentJobView,
            queue: queuedJobs
        );

        _userInterface.SetUiState(ent.Owner, CopyMachineUiKey.Key, state);
    }

    /// <summary>
    /// Checks whether the machine can create a copy from inserted paper or a buckled humanoid.
    /// </summary>
    public bool CanCopy(Entity<CopyMachineComponent> ent)
    {
        var hasPaperInCopySlot = ent.Comp.CopySlot.HasItem;
        var hasStrappedHumanoid = TryGetBuckledHumanoidAppearance(ent, out _);

        return hasStrappedHumanoid || hasPaperInCopySlot;
    }

    private void UpdateRunningAppearance(EntityUid uid, bool isRunning)
    {
        _appearance.SetData(uid, CopyMachineVisuals.IsRunning, isRunning);
    }
}
