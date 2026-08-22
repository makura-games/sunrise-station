using Content.Shared.Alert;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.Weapons.DualWield;

/// <summary>
///     Manages activation and deactivation of dual-wielding based on equipped weapons.
///     Applies dual-wield penalties via GunRefreshModifiersEvent.
/// </summary>
public sealed partial class SharedDualWieldSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedGunSystem _gunSystem = default!;

    private static readonly ProtoId<AlertPrototype> DualWieldAlertId = "DualWieldActive";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HandsComponent, DidEquipHandEvent>(OnHandEquipped);
        SubscribeLocalEvent<HandsComponent, DidUnequipHandEvent>(OnHandUnequipped);
        SubscribeLocalEvent<HandsComponent, HandCountChangedEvent>(OnHandCountChanged);
        SubscribeLocalEvent<DualWieldComponent, ComponentShutdown>(OnDualWieldShutdown);
        SubscribeLocalEvent<CanDualWieldComponent, GunRefreshModifiersEvent>(OnGunRefreshModifiers);
    }

    private void OnHandEquipped(Entity<HandsComponent> ent, ref DidEquipHandEvent args)
    {
        if (_timing.ApplyingState)
            return;

        CheckAndUpdateDualWield(ent);
    }

    private void OnHandUnequipped(Entity<HandsComponent> ent, ref DidUnequipHandEvent args)
    {
        if (_timing.ApplyingState)
            return;

        CheckAndUpdateDualWield(ent);
    }

    private void OnHandCountChanged(Entity<HandsComponent> ent, ref HandCountChangedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        CheckAndUpdateDualWield(ent);
    }

    private void OnDualWieldShutdown(Entity<DualWieldComponent> ent, ref ComponentShutdown args)
    {
        _alerts.ClearAlert(ent.Owner, DualWieldAlertId);
        RefreshDualWieldGuns(ent.Comp.LeftGun, ent.Comp.RightGun);
    }

    private void CheckAndUpdateDualWield(Entity<HandsComponent> ent)
    {
        if (!TryGetBothDualWieldGuns(ent, out var leftGun, out var rightGun))
        {
            RemComp<DualWieldComponent>(ent);
            return;
        }

        EnableDualWield(ent, leftGun, rightGun);
    }

    private bool TryGetBothDualWieldGuns(Entity<HandsComponent> ent, out EntityUid leftGun, out EntityUid rightGun)
    {
        leftGun = default;
        rightGun = default;

        foreach (var handName in _hands.EnumerateHands((ent.Owner, ent.Comp)))
        {
            if (!_hands.TryGetHeldItem((ent.Owner, ent.Comp), handName, out var held))
                continue;

            if (!HasComp<CanDualWieldComponent>(held))
                continue;

            if (!_hands.TryGetHand((ent.Owner, ent.Comp), handName, out var hand))
                continue;

            switch (hand.Value.Location)
            {
                case HandLocation.Left:
                    leftGun = held.Value;
                    break;
                case HandLocation.Right:
                    rightGun = held.Value;
                    break;
            }
        }

        if (!TryComp<CanDualWieldComponent>(leftGun, out var leftDualWield))
            return false;

        if (!TryComp<CanDualWieldComponent>(rightGun, out var rightDualWield))
            return false;

        if (leftDualWield.HandsRequired != rightDualWield.HandsRequired)
            return false;

        return ent.Comp.Count == leftDualWield.HandsRequired;
    }

    private void EnableDualWield(Entity<HandsComponent> ent, EntityUid leftGun, EntityUid rightGun)
    {
        var dualWield = EnsureComp<DualWieldComponent>(ent);
        dualWield.LeftGun = leftGun;
        dualWield.RightGun = rightGun;
        dualWield.GunQueue = new List<EntityUid> { leftGun, rightGun };
        Dirty(ent, dualWield);

        _alerts.ShowAlert(ent.Owner, DualWieldAlertId, severity: 0);
        RefreshDualWieldGuns(leftGun, rightGun);
    }

    /// <summary>
    ///     Applies dual-wield penalties to gun modifiers when the weapon's stats are refreshed.
    /// </summary>
    private void OnGunRefreshModifiers(Entity<CanDualWieldComponent> ent, ref GunRefreshModifiersEvent args)
    {
        var wielder = Transform(ent).ParentUid;
        if (wielder == EntityUid.Invalid)
            return;

        if (!TryComp<DualWieldComponent>(wielder, out var dualWield))
            return;

        if (dualWield.LifeStage > ComponentLifeStage.Running)
            return;

        if (dualWield.LeftGun != ent && dualWield.RightGun != ent)
            return;

        args.AngleIncrease *= (1f + ent.Comp.DualWieldInaccuracyPenalty);
        // Clamp penalty to [0, 0.99] so FireRate can never reach zero or go negative.
        var clampedPenalty = Math.Clamp(ent.Comp.DualWieldFireRatePenalty, 0f, 0.99f);
        args.FireRate *= (1f - clampedPenalty);
        // Safety floor: keep fire rate positive so downstream logic never divides by zero.
        args.FireRate = MathF.Max(args.FireRate, 0.1f);
        args.CameraRecoilScalar *= (1f + ent.Comp.DualWieldRecoilPenalty);
    }

    private void RefreshDualWieldGuns(EntityUid? leftGun, EntityUid? rightGun)
    {
        if (leftGun != null && Exists(leftGun.Value) && HasComp<GunComponent>(leftGun.Value))
            _gunSystem.RefreshModifiers(leftGun.Value);

        if (rightGun != null && Exists(rightGun.Value) && HasComp<GunComponent>(rightGun.Value))
            _gunSystem.RefreshModifiers(rightGun.Value);
    }
}
