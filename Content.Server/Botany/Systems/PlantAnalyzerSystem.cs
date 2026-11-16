using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Popups;
using Content.Shared.AbstractAnalyzer;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.PlantAnalyzer;
using Content.Shared.Botany.Systems;
using Content.Shared.Botany.Traits.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Labels.EntitySystems;
using Content.Shared.Paper;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Botany.Systems;

public sealed partial class PlantAnalyzerSystem : AbstractAnalyzerSystem<PlantAnalyzerComponent, PlantAnalyzerDoAfterEvent>
{
    [Dependency] private UserInterfaceSystem _uiSystem = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private PopupSystem _popupSystem = default!;
    [Dependency] private SharedHandsSystem _handsSystem = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private PaperSystem _paperSystem = default!;
    [Dependency] private LabelSystem _labelSystem = default!;
    [Dependency] private PlantAnalyzerLocalizationHelper _localizationHelper = default!;
    [Dependency] private PlantTraySystem _plantTray = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlantAnalyzerComponent, PlantAnalyzerPrintMessage>(OnPrint);
    }

    /// <inheritdoc/>
    public override void UpdateScannedUser(EntityUid analyzer, EntityUid target, bool scanMode)
    {
        if (!_uiSystem.HasUi(analyzer, PlantAnalyzerUiKey.Key))
            return;

        if (!ValidScanTarget(target))
            return;

        if (!_entityManager.TryGetComponent<PlantAnalyzerComponent>(analyzer, out var analyzerComponent))
            return;

        _uiSystem.ServerSendUiMessage(analyzer, PlantAnalyzerUiKey.Key, GatherData(analyzerComponent, scanMode, target: target));
    }

    private PlantAnalyzerScannedUserMessage GatherData(PlantAnalyzerComponent analyzer, bool? scanMode = null, EntityUid? target = null)
    {
        target ??= analyzer.ScannedEntity;
        PlantAnalyzerPlantData? plantData = null;
        PlantAnalyzerTrayData? trayData = null;
        PlantAnalyzerTolerancesData? tolerancesData = null;
        PlantAnalyzerProduceData? produceData = null;
        if (target is { } targetUid && _entityManager.TryGetComponent<PlantTrayComponent>(targetUid, out var tray))
        {
            trayData = new PlantAnalyzerTrayData(
                waterLevel: tray.WaterLevel,
                nutritionLevel: tray.NutritionLevel,
                toxins: tray.ToxinLevel,
                pestLevel: tray.PestLevel,
                weedLevel: tray.WeedLevel,
                chemicals: tray.SoilSolution?.Comp.Solution.Contents
                    .Select(reagent => reagent.Reagent.Prototype.ToString())
                    .ToList()
            );

            if (_plantTray.TryGetPlant((targetUid, tray), out var plantUid)
                && _entityManager.TryGetComponent<PlantHolderComponent>(plantUid.Value, out var plantHolder)
                && _entityManager.TryGetComponent<PlantComponent>(plantUid.Value, out var plant)
                && _entityManager.TryGetComponent<PlantDataComponent>(plantUid.Value, out var plantDataComponent))
            {
                plantData = new PlantAnalyzerPlantData(
                    seedDisplayName: plantDataComponent.Name,
                    health: plantHolder.Health,
                    endurance: plant.Endurance,
                    age: plantHolder.Age,
                    lifespan: plant.Lifespan,
                    dead: plantHolder.Dead,
                    viable: !_entityManager.HasComponent<PlantTraitUnviableComponent>(plantUid.Value),
                    mutating: plantHolder.MutationLevels.Values.Any(level => level > 0f),
                    kudzu: _entityManager.HasComponent<PlantTraitKudzuComponent>(plantUid.Value)
                );

                if (_entityManager.TryGetComponent<PlantGrowthComponent>(plantUid.Value, out var growth)
                    && _entityManager.TryGetComponent<PlantToxinsComponent>(plantUid.Value, out var toxins)
                    && _entityManager.TryGetComponent<PlantWeedPestComponent>(plantUid.Value, out var weedPest)
                    && _entityManager.TryGetComponent<PlantAtmosphericComponent>(plantUid.Value, out var atmospheric)
                    && _entityManager.TryGetComponent<PlantConsumeExudeGasComponent>(plantUid.Value, out var gases))
                {
                    tolerancesData = new PlantAnalyzerTolerancesData(
                        waterConsumption: growth.WaterConsumption,
                        nutrientConsumption: growth.NutrientConsumption,
                        toxinsTolerance: toxins.ToxinsTolerance,
                        pestTolerance: weedPest.PestTolerance,
                        weedTolerance: weedPest.WeedTolerance,
                        lowPressureTolerance: atmospheric.LowPressureTolerance,
                        highPressureTolerance: atmospheric.HighPressureTolerance,
                        lowHeatTolerance: atmospheric.LowHeatTolerance,
                        highHeatTolerance: atmospheric.HighHeatTolerance,
                        consumeGasses: [.. gases.ConsumeGasses.Keys]
                    );
                }

                _entityManager.TryGetComponent<PlantChemicalsComponent>(plantUid.Value, out var plantChemicals);
                _entityManager.TryGetComponent<PlantConsumeExudeGasComponent>(plantUid.Value, out var plantGases);

                var totalYield = 0;
                if (plant.Yield > 0 && plantDataComponent.ProductPrototypes.Count > 0)
                {
                    totalYield = plantHolder.YieldMod < 0
                        ? plant.Yield
                        : plant.Yield * plantHolder.YieldMod;
                    totalYield = Math.Max(1, totalYield);
                }

                produceData = new PlantAnalyzerProduceData(
                    yield: totalYield,
                    potency: plant.Potency,
                    chemicals: plantChemicals?.Chemicals.Keys.Select(id => id.Id).ToList() ?? [],
                    produce: plantDataComponent.ProductPrototypes,
                    exudeGasses: plantGases?.ExudeGasses.Keys.ToList() ?? [],
                    seedless: _entityManager.HasComponent<PlantTraitSeedlessComponent>(plantUid.Value)
                );
            }
        }

        return new PlantAnalyzerScannedUserMessage(
            GetNetEntity(target),
            scanMode,
            plantData,
            trayData,
            tolerancesData,
            produceData,
            analyzer.PrintReadyAt
        );
    }

    private void OnPrint(EntityUid uid, PlantAnalyzerComponent component, PlantAnalyzerPrintMessage args)
    {
        var user = args.Actor;

        if (_gameTiming.CurTime < component.PrintReadyAt)
        {
            // This shouldn't occur due to the UI guarding against it, but
            // if it does, tell the user why nothing happened.
            _popupSystem.PopupEntity(Loc.GetString("forensic-scanner-printer-not-ready"), uid, user);
            return;
        }

        // Spawn a piece of paper.
        var printed = Spawn(component.MachineOutput, Transform(uid).Coordinates);
        _handsSystem.PickupOrDrop(args.Actor, printed, checkActionBlocker: false);

        if (!TryComp<PaperComponent>(printed, out var paperComp))
        {
            Log.Error("Printed paper did not have PaperComponent.");
            return;
        }

        var data = GatherData(component);
        var missingData = Loc.GetString("plant-analyzer-printout-missing");

        var seedName = data.PlantData is not null ? Loc.GetString(data.PlantData.SeedDisplayName) : null;
        (string, object)[] parameters = [
            ("seedName", seedName ?? missingData),
            ("produce", data.ProduceData is not null ? _localizationHelper.ProduceToLocalizedStrings(data.ProduceData.Produce).Plural : missingData),
            ("water", data.TolerancesData?.WaterConsumption.ToString("0.00") ?? missingData),
            ("nutrients", data.TolerancesData?.NutrientConsumption.ToString("0.00") ?? missingData),
            ("toxins", data.TolerancesData?.ToxinsTolerance.ToString("0.00") ?? missingData),
            ("pests", data.TolerancesData?.PestTolerance.ToString("0.00") ?? missingData),
            ("weeds", data.TolerancesData?.WeedTolerance.ToString("0.00") ?? missingData),
            ("gasesIn", data.TolerancesData is not null ? _localizationHelper.GasesToLocalizedStrings(data.TolerancesData.ConsumeGasses) : missingData),
            ("kpa", data.TolerancesData?.IdealPressure.ToString("0.00") ?? missingData),
            ("kpaTolerance", data.TolerancesData?.PressureTolerance.ToString("0.00") ?? missingData),
            ("temp", data.TolerancesData?.IdealHeat.ToString("0.00") ?? missingData),
            ("tempTolerance", data.TolerancesData?.HeatTolerance.ToString("0.00") ?? missingData),
            ("yield", data.ProduceData?.Yield ?? -1),
            ("potency", data.ProduceData is not null ? Loc.GetString(data.ProduceData.Potency) : missingData),
            ("chemicals", data.ProduceData is not null ? _localizationHelper.ChemicalsToLocalizedStrings(data.ProduceData.Chemicals) : missingData),
            ("gasesOut", data.ProduceData is not null ? _localizationHelper.GasesToLocalizedStrings(data.ProduceData.ExudeGasses) : missingData),
            ("endurance", data.PlantData?.Endurance.ToString("0.00") ?? missingData),
            ("lifespan", data.PlantData?.Lifespan.ToString("0.00") ?? missingData),
            ("seeds", data.ProduceData is not null ? (data.ProduceData.Seedless ? "no" : "yes") : "other"),
            ("viable", data.PlantData is not null ? (data.PlantData.Viable ? "yes" : "no") : "other"),
            ("kudzu", data.PlantData is not null ? (data.PlantData.Kudzu ? "yes" : "no") : "other")
        ];

        _paperSystem.SetContent((printed, paperComp), Loc.GetString($"plant-analyzer-printout", [.. parameters]));
        _labelSystem.Label(printed, seedName);
        _audioSystem.PlayPvs(component.SoundPrint, uid,
            AudioParams.Default
            .WithVariation(0.25f)
            .WithVolume(3f)
            .WithRolloffFactor(2.8f)
            .WithMaxDistance(4.5f));

        component.PrintReadyAt = _gameTiming.CurTime + component.PrintCooldown;
    }

    /// <inheritdoc/>
    protected override Enum GetUiKey()
    {
        return PlantAnalyzerUiKey.Key;
    }

    /// <inheritdoc/>
    protected override bool ScanTargetPopupMessage(Entity<PlantAnalyzerComponent> uid, AfterInteractEvent args, [NotNullWhen(true)] out string? message)
    {
        message = null;
        return false;
    }

    /// <inheritdoc/>
    protected override bool ValidScanTarget(EntityUid? target)
    {
        return HasComp<PlantTrayComponent>(target);
    }
}
