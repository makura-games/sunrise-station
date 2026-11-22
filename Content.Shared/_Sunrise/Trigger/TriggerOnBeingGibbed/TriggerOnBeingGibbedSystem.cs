using Content.Shared.Gibbing;
using Content.Shared.Trigger.Systems;

namespace Content.Shared._Sunrise.Trigger.TriggerOnBeingGibbed;

public sealed partial class TriggerOnBeingGibbedSystem : EntitySystem
{
    [Dependency] private TriggerSystem _trigger = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TriggerOnBeingGibbedComponent, BeingGibbedEvent>(Trigger);
    }

    private void Trigger(Entity<TriggerOnBeingGibbedComponent> ent, ref BeingGibbedEvent args)
    {
        _trigger.Trigger(ent, key: ent.Comp.KeyOut);
    }
}
