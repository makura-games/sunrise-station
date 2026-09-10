using Content.Shared._Sunrise.Objectives.Components;
using Content.Shared._Sunrise.Objectives;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Records gun firing events from weapons observed by objective conditions.
/// </summary>
public sealed partial class GunShotObjectiveConditionSystem : ObjectiveEventConditionSystem<GunShotObjectiveCondition, ObjectiveCombatOwnerComponent, ObjectiveCombatObserverComponent>
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ObjectiveCombatObserverComponent, AmmoShotEvent>(OnAmmoShot);
    }

    private void OnAmmoShot(Entity<ObjectiveCombatObserverComponent> ent, ref AmmoShotEvent args)
    {
        if (args.Shooter is { } shooter)
        {
            RecordObservedEvent(ent, DefaultKey, shooter);
            return;
        }

        // У серверного события выстрела стрелок сейчас не заполняется, а у hitscan-оружия
        // нет выпущенного снаряда, по которому его можно было бы восстановить.
        var observers = new HashSet<EntityUid>(ent.Comp.Registrations.Values);
        foreach (var observer in observers)
        {
            if (!TryComp<HandsComponent>(observer, out var hands) ||
                !_hands.IsHolding((observer, hands), ent))
            {
                continue;
            }

            RecordObservedEvent(ent, DefaultKey, observer);
        }
    }
}

/// <summary>
/// Checks if the player has fired a gun (any gun, or a specific prototype).
/// </summary>
public sealed partial class GunShotObjectiveCondition : ObjectiveEventConditionBase<GunShotObjectiveCondition>
{
    public override bool ObserveAnyWithoutTarget => true;
}
