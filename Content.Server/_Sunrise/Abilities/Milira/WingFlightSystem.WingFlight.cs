using System;
using System.Linq;
using Content.Server.Actions;
using Content.Server.Popups;
using Content.Shared._Sunrise.Humanoid;
using Content.Shared.Body;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Toggleable;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared._Sunrise.Abilities.Milira;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Sunrise.Abilities.Milira;

/// <summary>
/// Серверная система для полёта расы милира, оно использует другую систему для изменения масштаба крыльев, а также изменяет маркинг, и тратит стамину.
/// </summary>
public sealed partial class WingFlightSystem : SharedWingFlightSystem
{
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SunriseHumanoidMarkingSystem _sunriseMarking = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WingFlightComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);
        SubscribeLocalEvent<WingFlightComponent, RefreshFrictionModifiersEvent>(OnRefreshFriction);
        SubscribeLocalEvent<WingFlightComponent, DownAttemptEvent>(OnDownAttempt);
        SubscribeLocalEvent<WingFlightComponent, KnockDownAttemptEvent>(OnKnockDownAttempt);
        SubscribeLocalEvent<WingFlightComponent, DownedEvent>(OnDowned);
        SubscribeLocalEvent<WingFlightComponent, KnockedDownEvent>(OnKnockedDown);
        SubscribeLocalEvent<WingFlightComponent, MobStateChangedEvent>(OnMobStateChanged);

        SubscribeLocalEvent<WingFlightComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<WingFlightComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<WingFlightComponent, ToggleActionEvent>(OnToggleAction);
    }

    private void OnComponentInit(Entity<WingFlightComponent> ent, ref ComponentInit args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.ActionEntity, ent.Comp.Action, ent.Owner);
        UpdateActionToggle(ent);
        ent.Comp.CurrentScaleMultiplier = Math.Max(ent.Comp.CurrentScaleMultiplier, ent.Comp.MinScaleMultiplier);
        Dirty(ent);
    }

    private void OnComponentRemove(Entity<WingFlightComponent> ent, ref ComponentRemove args)
    {
        if (ent.Comp.ActionEntity != null)
            _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);

        SetFlightEnabled(ent, false);
        DisableFlightPassability(ent);
        UpdateMarkings(ent, enable: false);
    }

    private void OnToggleAction(Entity<WingFlightComponent> ent, ref ToggleActionEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.ActionEntity == null || args.Action.Owner != ent.Comp.ActionEntity.Value)
            return;

        if (ent.Comp.FlightEnabled)
            args.Handled = DisableFlight(ent);
        else
            args.Handled = EnableFlight(ent);
    }

    private bool EnableFlight(Entity<WingFlightComponent> ent)
    {
        if (_standing.IsDown(ent.Owner))
            return false;

        if (!TryComp<StaminaComponent>(ent, out var stamina))
        {
            Activate(ent, 1f);
            return true;
        }

        var staminaPercent = GetStaminaPercent(stamina);
        if (staminaPercent < ent.Comp.ActivationThreshold)
        {
            _popup.PopupEntity(Loc.GetString("wing-flight-popup-not-enough-stamina"), ent, ent, PopupType.Medium);
            return false;
        }

        if (!_stamina.TryTakeStamina(ent, ent.Comp.ActivationStaminaDamage, stamina, visual: true))
        {
            _popup.PopupEntity(Loc.GetString("wing-flight-popup-activation-blocked"), ent, ent, PopupType.Small);
            return false;
        }

        Activate(ent, staminaPercent);
        return true;
    }

    private void Activate(Entity<WingFlightComponent> ent, float staminaPercent)
    {
        ent.Comp.SustainAccumulator = 0f;
        SetFlightEnabled(ent, true);
        UpdateActionToggle(ent);
        UpdateMarkings(ent, enable: true);
        SetScaleImmediate(ent, staminaPercent);
        EnableFlightPassability(ent);
    }

    private bool DisableFlight(Entity<WingFlightComponent> ent)
    {
        SetFlightEnabled(ent, false);
        UpdateActionToggle(ent);
        UpdateMarkings(ent, enable: false);
        DisableFlightPassability(ent);
        ent.Comp.SustainAccumulator = 0f;
        return true;
    }

    private void UpdateActionToggle(Entity<WingFlightComponent> ent)
    {
        if (ent.Comp.ActionEntity == null)
            return;

        _actions.SetToggled(ent.Comp.ActionEntity.Value, ent.Comp.FlightEnabled);
    }

    private void UpdateMarkings(Entity<WingFlightComponent> ent, bool enable)
    {
        if (!HasComp<VisualBodyComponent>(ent))
            return;

        if (!_sunriseMarking.TryGetLayerMarkings(ent, HumanoidVisualLayers.Tail, out var markings) ||
            markings.Count == 0)
        {
            return;
        }

        if (enable)
            EnableMarkings(ent, markings);
        else
            DisableMarkings(ent, markings);
    }

    private void EnableMarkings(Entity<WingFlightComponent> ent, List<Marking> markings)
    {
        ent.Comp.OriginalMarkings.Clear();

        var flightSuffix = ent.Comp.Suffix;
        var openSuffix = TryComp<WingToggleComponent>(ent, out var toggle) ? toggle.Suffix : null;

        for (var i = 0; i < markings.Count; i++)
        {
            var current = markings[i].MarkingId.Id;

            if (string.IsNullOrEmpty(current))
                continue;

            if (current.EndsWith(flightSuffix))
                continue;

            var desired = GetFlightMarkingId(current, flightSuffix, openSuffix);
            if (desired == null || !_prototype.HasIndex<MarkingPrototype>(desired))
                continue;

            ent.Comp.OriginalMarkings[i] = current;
            _sunriseMarking.SetMarkingId(ent, HumanoidVisualLayers.Tail, i, desired);
        }

        ent.Comp.AppliedMarkingOnEnable = ent.Comp.OriginalMarkings.Count > 0;
    }

    private void DisableMarkings(Entity<WingFlightComponent> ent, List<Marking> markings)
    {
        if (!ent.Comp.AppliedMarkingOnEnable || ent.Comp.OriginalMarkings.Count == 0)
            return;

        foreach (var (index, original) in ent.Comp.OriginalMarkings)
        {
            if (index < 0 || index >= markings.Count)
                continue;

            if (!_prototype.HasIndex<MarkingPrototype>(original))
                continue;

            if (markings[index].MarkingId.Id == original)
                continue;

            _sunriseMarking.SetMarkingId(ent, HumanoidVisualLayers.Tail, index, original);
        }

        ent.Comp.OriginalMarkings.Clear();
        ent.Comp.AppliedMarkingOnEnable = false;
    }

    private static string? GetFlightMarkingId(string current, string flightSuffix, string? openSuffix)
    {
        if (!string.IsNullOrEmpty(openSuffix) && current.EndsWith(openSuffix))
        {
            var baseId = current[..^openSuffix.Length];
            return $"{baseId}{flightSuffix}";
        }

        var desired = $"{current}{flightSuffix}";
        return desired == current ? null : desired;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ActiveWingFlightComponent, WingFlightComponent, StaminaComponent>();

        var toDisable = new List<Entity<WingFlightComponent>>();

        while (query.MoveNext(out var uid, out _, out var flightComp, out var stamina))
        {
            Entity<WingFlightComponent> ent = (uid, flightComp);
            var staminaPercent = GetStaminaPercent(stamina);

            if (ent.Comp.FlightEnabled)
            {
                if (staminaPercent <= ent.Comp.AutoDisableThreshold)
                {
                    toDisable.Add(ent);
                    SetScaleImmediate(ent, staminaPercent);
                    UpdateScale(ent, staminaPercent, frameTime);
                    continue;
                }

                var staminaCost = ent.Comp.SustainStaminaPerSecond * frameTime;
                if (!_stamina.TryTakeStamina(ent, staminaCost, stamina, visual: false))
                {
                    toDisable.Add(ent);
                    SetScaleImmediate(ent, staminaPercent);
                }
            }
            else
            {
                ent.Comp.SustainAccumulator = 0f;
            }

            UpdateScale(ent, staminaPercent, frameTime);
        }

        foreach (var ent in toDisable)
        {
            _popup.PopupEntity(Loc.GetString("wing-flight-popup-auto-disable"), ent, ent, PopupType.Medium);
            DisableFlight(ent);
        }
    }

    private void UpdateScale(Entity<WingFlightComponent> ent, float staminaPercent, float frameTime)
    {
        var target = ent.Comp.FlightEnabled
            ? GetTargetScale(ent, staminaPercent)
            : ent.Comp.MinScaleMultiplier;

        var t = 1f - MathF.Exp(-ent.Comp.ScaleLerpRate * frameTime);
        var newScale = MathHelper.Lerp(ent.Comp.CurrentScaleMultiplier, target, t);

        if (!MathHelper.CloseTo(newScale, ent.Comp.CurrentScaleMultiplier, 0.001f))
        {
            ent.Comp.CurrentScaleMultiplier = newScale;
            Dirty(ent);
        }
    }

    private void SetScaleImmediate(Entity<WingFlightComponent> ent, float staminaPercent)
    {
        var target = ent.Comp.FlightEnabled
            ? GetTargetScale(ent, staminaPercent)
            : ent.Comp.MinScaleMultiplier;

        if (!MathHelper.CloseTo(target, ent.Comp.CurrentScaleMultiplier, 0.001f))
        {
            ent.Comp.CurrentScaleMultiplier = target;
            Dirty(ent);
        }
    }

    private static float GetStaminaPercent(StaminaComponent stamina)
    {
        if (stamina.CritThreshold <= 0f)
            return 1f;

        var remaining = MathF.Max(0f, stamina.CritThreshold - stamina.StaminaDamage);
        return Math.Clamp(remaining / stamina.CritThreshold, 0f, 1f);
    }

    private void EnableFlightPassability(Entity<WingFlightComponent> ent)
    {
        if (!TryComp(ent, out PhysicsComponent? physics))
            return;

        EnsureComp<CanMoveInAirComponent>(ent);
        _physics.SetBodyStatus(ent, physics, BodyStatus.InAir);

        if (!TryComp(ent, out FixturesComponent? fixtures))
            return;

        var fixtureIds = fixtures.Fixtures.Keys.ToArray();
        for (var i = fixtureIds.Length - 1; i >= 0; i--)
        {
            var id = fixtureIds[i];
            if (!fixtures.Fixtures.TryGetValue(id, out var fixture))
                continue;

            ent.Comp.OriginalCollisionMasks.TryAdd(id, fixture.CollisionMask);
            ent.Comp.OriginalCollisionLayers.TryAdd(id, fixture.CollisionLayer);
            _physics.RemoveCollisionMask(ent, id, fixture, (int)CollisionGroup.MidImpassable, manager: fixtures);
        }
    }

    private void DisableFlightPassability(Entity<WingFlightComponent> ent)
    {
        RemCompDeferred<CanMoveInAirComponent>(ent);

        if (TryComp<PhysicsComponent>(ent, out var physics))
            _physics.SetBodyStatus(ent, physics, BodyStatus.OnGround);

        if (TryComp<FixturesComponent>(ent, out var fixtures))
        {
            // Обратный обход фикстур для избежания изменения коллекции во время перечисления
            var fixtureIds = fixtures.Fixtures.Keys.ToArray();
            for (var i = fixtureIds.Length - 1; i >= 0; i--)
            {
                var id = fixtureIds[i];
                if (!fixtures.Fixtures.TryGetValue(id, out var fixture))
                    continue;

                if (ent.Comp.OriginalCollisionMasks.TryGetValue(id, out var mask))
                    _physics.SetCollisionMask(ent, id, fixture, mask, manager: fixtures);

                if (ent.Comp.OriginalCollisionLayers.TryGetValue(id, out var layer))
                    _physics.SetCollisionLayer(ent, id, fixture, layer, manager: fixtures);
            }
        }

        ent.Comp.OriginalCollisionMasks.Clear();
        ent.Comp.OriginalCollisionLayers.Clear();
    }

    private void OnMobStateChanged(Entity<WingFlightComponent> ent, ref MobStateChangedEvent args)
    {
        if (!ent.Comp.FlightEnabled)
            return;

        if (args.NewMobState == MobState.Critical)
            DisableFlight(ent);
    }
}
