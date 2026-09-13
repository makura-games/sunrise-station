using Content.Server.Body.Systems;
using Content.Shared._Sunrise.CarpQueen;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.CarpQueen;

/// <summary>
/// Система обрабатывает укусы карпов-слуг:
/// вводит 1 единицу каждого реагента из жидкости, в которой вылупился карп.
/// </summary>
public sealed partial class CarpServantBiteSystem : EntitySystem
{
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private IPrototypeManager _protos = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CarpServantMemoryComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(EntityUid uid, CarpServantMemoryComponent memory, MeleeHitEvent args)
    {
        if (memory.RememberedReagents.Count == 0)
            return;

        // Вводим 1 единицу каждого запомненного реагента в каждую пораженную цель.
        foreach (var target in args.HitEntities)
        {
            if (!TryComp<BloodstreamComponent>(target, out var bloodstream))
                continue;

            // Создаем раствор с настроенным количеством каждого запомненного реагента.
            var solution = new Solution();
            foreach (var (reagentId, _) in memory.RememberedReagents)
            {
                if (_protos.HasIndex<ReagentPrototype>(reagentId))
                {
                    solution.AddReagent(reagentId, memory.BiteReagentAmount);
                }
            }

            if (solution.Volume > FixedPoint2.Zero)
            {
                _bloodstream.TryAddToBloodstream((target, bloodstream), solution);
            }
        }
    }
}
