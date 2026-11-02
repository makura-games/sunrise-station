using System.Linq;
using Content.Shared.Ensnaring;
using Content.Shared.Ensnaring.Components;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Systems;
using Content.Shared._Sunrise.Trigger;
using Content.Shared.Trigger.Components.Effects;

namespace Content.Shared._Sunrise.Trigger;

public sealed partial class UnsnareOnTriggerSystem : EntitySystem
{
    [Dependency] private SharedEnsnareableSystem _ensnareable = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<UnsnareOnTriggerComponent, TriggerEvent>(OnTrigger);
    }

    private void OnTrigger(Entity<UnsnareOnTriggerComponent> ent, ref TriggerEvent args)
    {
        if (args.Key != null && !ent.Comp.KeysIn.Contains(args.Key))
            return;

        var target = ent.Comp.TargetUser ? args.User : ent.Owner;

        if (target == null)
            return;

        if (!TryComp<EnsnareableComponent>(target.Value, out var ensnareable) ||
            ensnareable.Container is not { } container ||
            !_ensnareable.IsEnsnared((target.Value, ensnareable)))
            return;

        foreach (var ensnareEntity in container.ContainedEntities.ToList())
        {
            if (!TryComp<EnsnaringComponent>(ensnareEntity, out var ensnaring))
                continue;

            _ensnareable.ForceFree((ensnareEntity, ensnaring));

            if (ensnaring.DestroyOnRemove)
                PredictedQueueDel(ensnareEntity);
        }

        args.Handled = true;
    }
}
