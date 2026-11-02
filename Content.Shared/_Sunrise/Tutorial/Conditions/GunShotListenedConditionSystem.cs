using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Shared._Sunrise.Tutorial.Conditions;

/// <summary>
/// Records gun firing events from observed weapons for tutorial conditions.
/// </summary>
public sealed partial class GunShotListenedConditionSystem : EventListenedConditionSystemBase<GunShotListenedCondition>
{
    [Dependency] private SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TutorialObservableComponent, AmmoShotEvent>(OnAmmoShot);
    }

    private void OnAmmoShot(Entity<TutorialObservableComponent> ent, ref AmmoShotEvent args)
    {
        if (args.Shooter is { } shooter)
        {
            if (ent.Comp.Observers.Contains(shooter))
                RecordEvent(shooter, DefaultKey, ent);

            return;
        }

        // У серверного события выстрела стрелок сейчас не заполняется, а у hitscan-оружия
        // нет выпущенного снаряда, по которому его можно было бы восстановить.
        foreach (var observer in ent.Comp.Observers)
        {
            if (!TryComp<HandsComponent>(observer, out var hands) ||
                !_hands.IsHolding((observer, hands), ent))
            {
                continue;
            }

            RecordEvent(observer, DefaultKey, ent);
        }
    }
}

/// <summary>
/// Checks if the player has fired a gun (any gun, or a specific prototype).
/// </summary>
public sealed partial class GunShotListenedCondition : EventListenedConditionBase<GunShotListenedCondition>
{
    public override bool ObserveAnyWithoutTarget => true;
}
