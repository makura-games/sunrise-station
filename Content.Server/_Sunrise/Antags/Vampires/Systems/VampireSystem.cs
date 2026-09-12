using Content.Server.Actions;
using Content.Server.Antag;
using Content.Server.Damage.Systems;
using Content.Server.Interaction;
using Content.Server.Popups;
using Content.Server.Stunnable;
using Content.Server._Sunrise.Antags.Vampires.Components;
using Content.Shared._Sunrise.Antags.Vampires.Components;
using Content.Shared._Sunrise.Antags.Vampires.Events;
using Content.Shared._Sunrise.Antags.Vampires.Systems;
using Content.Shared.Actions.Components;
using Content.Shared.Alert;
using Content.Shared.Body;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Metabolism;
using Content.Shared.Movement.Systems;
using Content.Shared.Objectives.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Antags.Vampires.Systems;

public sealed partial class VampireSystem : SharedVampireSystem
{
    // Инициализация и обновление вампиров.

    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly StunSystem _stun = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ILogManager _log = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly InteractionSystem _interaction = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    private ISawmill _sawmill = null!;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _log.GetSawmill("Vampire");

        // Conversion
        SubscribeLocalEvent<BloodDrainConditionComponent, ObjectiveGetProgressEvent>(OnBloodDrainGetProgress);

        // Lifecycle
        SubscribeLocalEvent<VampireComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<VampireComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<MetabolizerComponent, BodyRelayedEvent<SetVampireMetabolismEvent>>(OnSetVampireMetabolism);

        // Actions
        SubscribeLocalEvent<ActionsComponent, ComponentStartup>(OnActionsComponentStartup);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);

        // Feeding
        SubscribeLocalEvent<VampireComponent, VampireToggleFangsActionEvent>(OnToggleFangs);
        SubscribeLocalEvent<VampireComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<VampireComponent, VampireDrinkBloodDoAfterEvent>(OnDrinkDoAfter);

        // Sleep
        SubscribeLocalEvent<VampireComponent, VampireSleepActionEvent>(OnSleep);
        SubscribeLocalEvent<VampireComponent, DoAfterAttemptEvent<VampireSleepDoAfterEvent>>(OnSleepDoAfterAttempt);
        SubscribeLocalEvent<VampireComponent, VampireSleepDoAfterEvent>(OnSleepDoAfter);

        // Glare
        SubscribeLocalEvent<VampireComponent, VampireGlareActionEvent>(OnGlare);

        // Rejuvenation
        SubscribeLocalEvent<VampireComponent, VampireRejuvenateIActionEvent>(OnRejuvenate);
        SubscribeLocalEvent<VampireComponent, VampireRejuvenateIiActionEvent>(OnRejuvenateUpgraded);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<VampireComponent, VampireFeedingComponent>();
        while (query.MoveNext(out var uid, out var vampire, out var feeding))
        {
            if (now <= feeding.NextUpdate)
                continue;

            var elapsed = feeding.LastUpdate == TimeSpan.Zero
                ? (float)feeding.UpdateDelay.TotalSeconds
                : MathF.Max(0f, (float)(now - feeding.LastUpdate).TotalSeconds);

            feeding.LastUpdate = now;
            feeding.NextUpdate = now + feeding.UpdateDelay;

            var ent = (uid, vampire);
            ProcessBloodDecay(ent, feeding, elapsed);
            HandleHolyWater(ent);
            HandleHolyPlace(ent);
        }

        ProcessActiveRejuvenation(now);
    }
}
