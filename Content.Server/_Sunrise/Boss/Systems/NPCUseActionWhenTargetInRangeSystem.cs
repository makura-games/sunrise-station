using Content.Server._Sunrise.Boss.Components;
using Content.Server.NPC.HTN;
using Content.Shared._Sunrise.Boss.Components;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Atmos.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Boss.Systems;

public sealed partial class NPCUseActionWhenTargetInRangeSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<NPCUseActionWhenTargetInRangeComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<NPCUseActionWhenTargetInRangeComponent> ent, ref MapInitEvent args)
    {
        foreach (var action in ent.Comp.Actions)
        {
            action.ActionEnt = _actions.AddAction(ent, action.ActionId);
            // Костыль. Так как это писалось для MobHellspawn, а у него добавление действия пождигания обрабатывает компонент Firestarter, то надо из того компонента выудить энтити экшена
            if (action.ActionId == "ActionFireStarter")
            {
                if (TryComp<FirestarterComponent>(ent, out var firestarterComponent))
                    action.ActionEnt = firestarterComponent.FireStarterActionEntity;
            }

            if (action.ActionId == "ActionHellSpawnRush")
            {
                if (TryComp<HellSpawnRushComponent>(ent, out var hellSpawnRushComponent))
                    action.ActionEnt = hellSpawnRushComponent.RushActionEntity;
            }

            if (action.ActionId == "ActionHellSpawnTentacleRight")
            {
                if (TryComp<HellSpawnTentacleComponent>(ent, out var hellSpawnTentacleComponent))
                {
                    action.ActionEnt = hellSpawnTentacleComponent.RightGrabActionEntity;
                }
            }

            if (action.ActionId == "ActionHellSpawnTentacleLeft")
            {
                if (TryComp<HellSpawnTentacleComponent>(ent, out var hellSpawnTentacleComponent))
                {
                    action.ActionEnt = hellSpawnTentacleComponent.LeftGrabActionEntity;
                }
            }

            if (action.ActionEnt == null || !TryComp<ActionComponent>(action.ActionEnt, out var actionComp))
                continue;

            if (actionComp.UseDelay is not { } useDelay || useDelay <= TimeSpan.Zero)
                continue;

            // Spread initial cooldowns so NPCs do not unload all granted actions at spawn.
            var randomDelay = TimeSpan.FromSeconds(_random.NextFloat() * (float) useDelay.TotalSeconds);
            if (randomDelay > TimeSpan.Zero)
                _actions.SetCooldown(action.ActionEnt, randomDelay);
        }
    }

    public bool TryUseAction(Entity<NPCUseActionWhenTargetInRangeComponent?> user, EntityUid target)
    {
        if (!Resolve(user, ref user.Comp, false))
            return false;

        foreach (var actionWhenTargetInRange in user.Comp.Actions)
        {
            if (TryComp<InstantActionComponent>(actionWhenTargetInRange.ActionEnt, out var instantAction))
            {
                var action = Comp<ActionComponent>(actionWhenTargetInRange.ActionEnt.Value);

                if (!_actions.ValidAction((actionWhenTargetInRange.ActionEnt.Value, action)))
                    continue;

                var targetXform = Transform(target);
                var userXform = Transform(user);

                // Манхеттенская геометрия
                var distance = Math.Abs(targetXform.Coordinates.X - userXform.Coordinates.X) +
                               Math.Abs(targetXform.Coordinates.Y - userXform.Coordinates.Y);
                if (actionWhenTargetInRange.MaxRange != 0f && distance > actionWhenTargetInRange.MaxRange ||
                    actionWhenTargetInRange.MinRange != 0f && distance < actionWhenTargetInRange.MinRange)
                    continue;

                _actions.PerformAction((user, null),
                    (actionWhenTargetInRange.ActionEnt.Value, action),
                    instantAction.Event,
                    false);
            }
            else if (TryComp<WorldTargetActionComponent>(actionWhenTargetInRange.ActionEnt, out _))
            {
                var action = Comp<ActionComponent>(actionWhenTargetInRange.ActionEnt.Value);

                if (!_actions.ValidAction((actionWhenTargetInRange.ActionEnt.Value, action)))
                    continue;

                var targetXform = Transform(target);
                var userXform = Transform(user);

                // Манхеттенская геометрия
                var distance = Math.Abs(targetXform.Coordinates.X - userXform.Coordinates.X) +
                               Math.Abs(targetXform.Coordinates.Y - userXform.Coordinates.Y);
                if (actionWhenTargetInRange.MaxRange != 0f && distance > actionWhenTargetInRange.MaxRange ||
                    actionWhenTargetInRange.MinRange != 0f && distance < actionWhenTargetInRange.MinRange)
                    continue;

                _actions.SetEventTarget(actionWhenTargetInRange.ActionEnt.Value, target);

                _actions.PerformAction((user, null),
                    (actionWhenTargetInRange.ActionEnt.Value, action),
                    null,
                    false);
            }
        }

        return true;
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<NPCUseActionWhenTargetInRangeComponent, HTNComponent>();
        while (query.MoveNext(out var uid, out var comp, out var htn))
        {
            if (!htn.Blackboard.TryGetValue<EntityUid>(comp.TargetKey, out var target, EntityManager))
                continue;

            if (comp.Delay != null)
            {
                if (_timing.CurTime - comp.Prev < comp.Delay)
                    continue;
            }

            if (comp.Delay != null && TryUseAction((uid, comp), target))
                comp.Prev = _timing.CurTime;
        }
    }
}
