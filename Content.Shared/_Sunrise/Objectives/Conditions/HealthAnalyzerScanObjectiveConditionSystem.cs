using Content.Shared._Sunrise.Objectives.Components;
using Content.Shared._Sunrise.Objectives;
using Content.Shared._Sunrise.Research.Artifact;

namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Отслеживает завершённое сканирование сущности анализатором здоровья.
/// </summary>
public sealed partial class HealthAnalyzerScanObjectiveConditionSystem
    : ObjectiveEventConditionSystem<HealthAnalyzerScanObjectiveCondition, ObjectiveHealthOwnerComponent, ObjectiveHealthObserverComponent>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ObjectiveHealthObserverComponent, EntityAnalyzedEvent>(OnEntityAnalyzed);
    }

    private void OnEntityAnalyzed(Entity<ObjectiveHealthObserverComponent> ent, ref EntityAnalyzedEvent args)
    {
        RecordObservedEvent(ent, DefaultKey);
    }
}

/// <summary>
/// Проверяет, что игрок завершил сканирование указанной сущности.
/// </summary>
public sealed partial class HealthAnalyzerScanObjectiveCondition
    : ObjectiveEventConditionBase<HealthAnalyzerScanObjectiveCondition>
{
}
