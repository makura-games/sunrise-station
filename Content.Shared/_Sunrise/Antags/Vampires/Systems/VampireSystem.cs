using Content.Shared.Actions.Components;
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
using Content.Shared._Sunrise.Antags.Vampires.Events;
using Content.Shared._Sunrise.Body.Components;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.Antags.Vampires.Systems;

/// <summary>
/// Predicts vampire abilities, feeding, blood reserves and power progression.
/// </summary>
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

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(SharedDoAfterSystem));
        InitializeInteractions();

        SubscribeLocalEvent<VampireComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<VampireComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<VampireComponent, AfterAutoHandleStateEvent>(OnAfterState);
        SubscribeLocalEvent<BloodSourceComponent, EntityTerminatingEvent>(OnBloodSourceTerminating);

        SubscribeLocalEvent<ActionsComponent, ComponentStartup>(OnActionsComponentStartup);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<VampireComponent, VampireToggleFangsActionEvent>(OnToggleFangs);
        SubscribeLocalEvent<VampireComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<VampireComponent, VampireDrinkBloodDoAfterEvent>(OnDrinkDoAfter);
        SubscribeLocalEvent<VampireComponent, VampireSleepActionEvent>(OnSleep);
        SubscribeLocalEvent<VampireComponent, DoAfterAttemptEvent<VampireSleepDoAfterEvent>>(OnSleepDoAfterAttempt);
        SubscribeLocalEvent<VampireComponent, VampireSleepDoAfterEvent>(OnSleepDoAfter);
        SubscribeLocalEvent<VampireComponent, VampireGlareActionEvent>(OnGlare);
        SubscribeLocalEvent<VampireComponent, VampireRejuvenateIActionEvent>(OnRejuvenate);
        SubscribeLocalEvent<VampireComponent, VampireRejuvenateIiActionEvent>(OnRejuvenateUpgraded);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Состояние пересчитывается и при повторном предикте; таймеры откатываются из component state.
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<VampireFeedingComponent, VampireComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var feeding, out var vampire, out var metadata))
        {
            if (metadata.EntityPaused || now <= feeding.NextUpdate)
                continue;

            var elapsed = feeding.LastUpdate == TimeSpan.Zero
                ? (float) feeding.UpdateDelay.TotalSeconds
                : MathF.Max(0f, (float) (now - feeding.LastUpdate).TotalSeconds);

            feeding.LastUpdate = now;
            feeding.NextUpdate = now + feeding.UpdateDelay;
            ProcessBloodDecay((uid, vampire), feeding, elapsed);
            Dirty(uid, feeding);
        }

        ProcessActiveRejuvenation(now);
    }
}
