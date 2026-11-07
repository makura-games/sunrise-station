using Content.Shared.Actions;
using Content.Shared.Alert;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared._Sunrise.Antags.Vampires.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.Antags.Vampires.Systems;

public sealed partial class VampireSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityQuery<VampireConfigurationComponent> _configurationQuery = default!;
    [Dependency] private EntityQuery<VampireFeedingComponent> _feedingQuery = default!;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(SharedDoAfterSystem));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Состояние пересчитывается и при повторном предикте; таймеры восстанавливаются из сетевого состояния компонента.
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<VampireFeedingComponent, VampireComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var feeding, out var vampire, out var metadata))
        {
            if (metadata.EntityPaused || now <= vampire.NextBloodUpdate ||
                !TryGetPowerLevelPrototype(vampire.PowerLevel, out var level))
                continue;

            var elapsed = vampire.LastBloodUpdate == TimeSpan.Zero
                ? (float)feeding.UpdateDelay.TotalSeconds
                : MathF.Max(0f, (float)(now - vampire.LastBloodUpdate).TotalSeconds);

            vampire.LastBloodUpdate = now;
            vampire.NextBloodUpdate = now + feeding.UpdateDelay;
            DirtyField(uid, vampire, nameof(VampireComponent.LastBloodUpdate));
            DirtyField(uid, vampire, nameof(VampireComponent.NextBloodUpdate));
            ProcessBloodDecay((uid, vampire), feeding, level, elapsed);
        }

        ProcessActiveRejuvenation(now);
    }
}
