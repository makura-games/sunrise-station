using Content.Server.Antag;
using Content.Server._Sunrise.Antags.Vampires.Components;
using Content.Shared.Alert;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared._Sunrise.Antags.Vampires.Components;
using Content.Shared._Sunrise.Antags.Vampires.Events;
using Content.Shared._Sunrise.Antags.Vampires.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Antags.Vampires.Systems;

/// <summary>
/// Управляет ролями, целями, сообщениями и уязвимостями вампиров к святыням на сервере.
/// </summary>
public sealed partial class ServerVampireSystem : EntitySystem
{
    [Dependency] private VampireSystem _vampire = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(VampireSystem));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<VampireHolyComponent, VampireComponent, MetaDataComponent>();
        var now = _timing.CurTime;
        while (query.MoveNext(out var uid, out var holy, out var vampire, out var metadata))
        {
            if (metadata.EntityPaused || now <= holy.NextUpdate)
                continue;

            holy.NextUpdate = now + (TryComp<VampireFeedingComponent>(uid, out var feeding)
                ? feeding.UpdateDelay
                : TimeSpan.FromSeconds(1));

            HandleHolyWater((uid, vampire));
            HandleHolyPlace((uid, vampire));
        }
    }

    [SubscribeLocalEvent]
    public void OnPowerLevelChanged(Entity<VampireComponent> ent, ref VampirePowerLevelChangedEvent args)
    {
        if (TryComp<VampireConfigurationComponent>(ent, out var configuration) &&
            configuration.PowerLevelMessages.TryGetValue(args.Level, out var message))
        {
            _antag.SendBriefing(ent, Loc.GetString(message), Color.Crimson, null);
        }
    }
}
