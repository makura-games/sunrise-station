using Content.Server._Sunrise.BloodCult.Items.Components;
using Content.Server.Hands.Systems;
using Content.Server.Stunnable;
using Content.Shared._Sunrise.BloodCult.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Throwing;

namespace Content.Server._Sunrise.BloodCult.Items.Systems;

public sealed partial class ReturnItemOnThrowSystem : EntitySystem
{
    [Dependency] private HandsSystem _hands = default!;
    [Dependency] private StunSystem _stun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReturnItemOnThrowComponent, ThrowDoHitEvent>(OnThrowHit);
    }

    private void OnThrowHit(EntityUid uid, ReturnItemOnThrowComponent component, ThrowDoHitEvent args)
    {
        var isCultist = HasComp<BloodCultistComponent>(args.Target);
        var thrower = args.Component.Thrower;
        if (!HasComp<BloodCultistComponent>(thrower))
            return;

        if (!HasComp<MobStateComponent>(args.Target))
            return;

        if (!_stun.IsParalyzed(args.Target))
        {
            if (!isCultist)
            {
                _stun.TryAddParalyzeDuration(args.Target, TimeSpan.FromSeconds(component.StunTime));
            }
        }

        _hands.PickupOrDrop(thrower, uid);
    }
}
