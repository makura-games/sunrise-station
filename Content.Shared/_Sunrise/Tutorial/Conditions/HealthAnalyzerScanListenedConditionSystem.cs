using Content.Shared._Sunrise.Research.Artifact;
using Content.Shared._Sunrise.Tutorial.Components;

namespace Content.Shared._Sunrise.Tutorial.Conditions;

/// <summary>
/// Отслеживает завершённое сканирование сущности анализатором здоровья.
/// </summary>
public sealed partial class HealthAnalyzerScanListenedConditionSystem
    : EventListenedConditionSystemBase<HealthAnalyzerScanListenedCondition>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TutorialObservableComponent, EntityAnalyzedEvent>(OnEntityAnalyzed);
    }

    private void OnEntityAnalyzed(Entity<TutorialObservableComponent> ent, ref EntityAnalyzedEvent args)
    {
        foreach (var observer in ent.Comp.Observers)
        {
            if (TerminatingOrDeleted(observer))
                continue;

            RecordEvent(observer, DefaultKey, ent);
        }
    }
}

/// <summary>
/// Проверяет, что игрок завершил сканирование указанной сущности.
/// </summary>
public sealed partial class HealthAnalyzerScanListenedCondition
    : EventListenedConditionBase<HealthAnalyzerScanListenedCondition>
{
}
