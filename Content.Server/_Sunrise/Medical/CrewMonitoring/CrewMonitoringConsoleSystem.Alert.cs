using Content.Server.Power.EntitySystems;
using Content.Shared.Medical.CrewMonitoring;
using Content.Shared.Morgue.Components;
using Content.Shared.Power.Components;
using Content.Shared.PowerCell.Components;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Medical.CrewMonitoring;

public sealed partial class CrewMonitoringConsoleSystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private ILocalizationManager _loc = default!;
    [Dependency] private PowerReceiverSystem _powerReceiver = default!;

    private const float CriticalDamagePercentage = 1.0f;

    private void InitializeAlert()
    {
        SubscribeLocalEvent<CrewMonitoringConsoleComponent, MapInitEvent>(OnAlertMapInit);
        SubscribeLocalEvent<CrewMonitoringCorpseAlertComponent, CrewMonitoringToggleCorpseAlertMessage>(OnToggleCorpseAlert);
        SubscribeLocalEvent<CrewMonitoringCorpseAlertComponent, GetVerbsEvent<InteractionVerb>>(AddToggleVerb);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _gameTiming.CurTime;
        var query = EntityQueryEnumerator<CrewMonitoringConsoleComponent, CrewMonitoringCorpseAlertComponent>();
        while (query.MoveNext(out var uid, out var console, out var alert))
        {
            if (!alert.DoCorpseAlert || alert.NextCorpseAlertTime > curTime)
                continue;

            alert.NextCorpseAlertTime = curTime + TimeSpan.FromSeconds(alert.CorpseAlertTime);

            if (!HasCorpsesOutsideMorgue(console))
                continue;

            TryPlayCorpseAlertSound(uid, alert);
        }
    }

    private void TryPlayCorpseAlertSound(EntityUid uid, CrewMonitoringCorpseAlertComponent alert)
    {
        if (HasComp<ActivatableUIRequiresPowerCellComponent>(uid) && TryComp<PowerCellDrawComponent>(uid, out _) && _cell.HasActivatableCharge(uid))
        {
            _audio.PlayPvs(alert.CorpseAlertSound, uid);
            return;
        }

        if (HasComp<ActivatableUIRequiresPowerComponent>(uid) && _powerReceiver.IsPowered(uid))
            _audio.PlayPvs(alert.CorpseAlertSound, uid);
    }

    private void OnAlertMapInit(EntityUid uid, CrewMonitoringConsoleComponent component, MapInitEvent args)
    {
        EnsureComp<CrewMonitoringCorpseAlertComponent>(uid);
    }

    private bool HasCorpsesOutsideMorgue(CrewMonitoringConsoleComponent component)
    {
        foreach (var sensor in component.ConnectedSensors.Values)
        {
            var damagePercentage = sensor.DamagePercentage;
            var isCritical = damagePercentage.HasValue && damagePercentage.Value >= CriticalDamagePercentage;

            if (sensor.IsAlive && !isCritical)
                continue;

            if (!TryGetEntity(sensor.OwnerUid, out var ownerUid))
                continue;

            if (!IsEntityInMorgue(ownerUid.Value))
                return true;
        }

        return false;
    }

    private bool IsEntityInMorgue(EntityUid entity)
    {
        var parent = Transform(entity).ParentUid;
        return HasComp<MorgueComponent>(parent);
    }

    private void OnToggleCorpseAlert(Entity<CrewMonitoringCorpseAlertComponent> ent, ref CrewMonitoringToggleCorpseAlertMessage args)
    {
        ToggleAlert(ent);
    }

    private void AddToggleVerb(Entity<CrewMonitoringCorpseAlertComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!ent.Comp.ShowToggleVerb)
            return;

        if (!args.CanInteract || !args.CanAccess)
            return;

        var text = _loc.GetString(ent.Comp.DoCorpseAlert
            ? "item-toggle-deactivate-alert"
            : "item-toggle-activate-alert");

        args.Verbs.Add(new InteractionVerb
        {
            Text = text,
            Act = () => ToggleAlert(ent),
        });
    }

    public void ToggleAlert(Entity<CrewMonitoringCorpseAlertComponent> ent)
    {
        ent.Comp.DoCorpseAlert = !ent.Comp.DoCorpseAlert;
        Dirty(ent.Owner, ent.Comp);
        UpdateUserInterface(ent.Owner);
    }
}
