using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared.CombatMode;
using Content.Shared.Damage.Systems;

namespace Content.Shared._Sunrise.Tutorial.Conditions;

/// <summary>
/// Отслеживает успешные попытки обезоруживания игрока туториала.
/// </summary>
public sealed partial class DisarmListenedConditionSystem
    : EventListenedConditionSystemBase<DisarmListenedCondition>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TutorialObservableComponent, DisarmedEvent>(
            OnDisarmed,
            after: [typeof(SharedStaminaSystem)]);
    }

    private void OnDisarmed(Entity<TutorialObservableComponent> ent, ref DisarmedEvent args)
    {
        if (!args.Handled || !ent.Comp.Observers.Contains(args.Source))
            return;

        RecordEvent(args.Source, DefaultKey, ent);
    }
}

/// <summary>
/// Проверяет, что игрок успешно применил обезоруживание к указанной сущности.
/// </summary>
public sealed partial class DisarmListenedCondition : EventListenedConditionBase<DisarmListenedCondition>;
