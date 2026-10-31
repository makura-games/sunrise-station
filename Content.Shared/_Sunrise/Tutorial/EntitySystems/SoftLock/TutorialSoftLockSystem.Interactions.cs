using System;
using System.Collections.Generic;
using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared._Sunrise.Tutorial.Prototypes;
using Content.Shared.Buckle.Components;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Nutrition;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.Events;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared.Item;
using Content.Shared.UserInterface;
using Content.Shared.Wieldable;

namespace Content.Shared._Sunrise.Tutorial.EntitySystems.SoftLock;

/// <summary>
///     Blocks interactions.
/// </summary>
public sealed partial class TutorialSoftLockSystem
{
    public void InitializeInteractions()
    {
        SubscribeLocalEvent<TutorialInteractSoftLockComponent, InteractionAttemptEvent>(OnHeldItemInteractionAttempt);
        SubscribeLocalEvent<TutorialAttackSoftLockComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<TutorialAttackSoftLockComponent, ShotAttemptedEvent>(OnShotAttempted);
        SubscribeLocalEvent<TutorialBuckleSoftLockComponent, BuckleAttemptEvent>(OnBuckleAttempt);
        SubscribeLocalEvent<TutorialWieldSoftLockComponent, WieldAttemptEvent>(OnWieldAttempt);

        SubscribeLocalEvent<TutorialSoftLockEntityComponent, EdibleEvent>(OnEdible);
        SubscribeLocalEvent<TutorialSoftLockEntityComponent, ActivatableUIOpenAttemptEvent>(OnBuiOpen);
        SubscribeLocalEvent<TutorialIngestSoftLockComponent, TutorialShouldMarkEntityEvent>(OnIngestShouldMarkEntity);
        SubscribeLocalEvent<TutorialOpenUiSoftLockComponent, TutorialShouldMarkEntityEvent>(OnOpenUiShouldMarkEntity);
    }

    private void OnBuckleAttempt(Entity<TutorialBuckleSoftLockComponent> ent, ref BuckleAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        args.Cancelled = true;
        if (args.Popup)
            ShowPopup(args.User ?? ent.Owner, ent.Comp.Popup);
    }

    private void OnWieldAttempt(Entity<TutorialWieldSoftLockComponent> ent, ref WieldAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        args.Cancel();
        ShowPopup(ent, ent.Comp.Popup);
    }

    private void OnAttackAttempt(Entity<TutorialAttackSoftLockComponent> ent, ref AttackAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        TryBlockAttack(ent, args);
    }

    private void OnShotAttempted(Entity<TutorialAttackSoftLockComponent> ent, ref ShotAttemptedEvent args)
    {
        if (args.Cancelled)
            return;

        TryBlockShot(ent, ref args);
    }

    private void OnHeldItemInteractionAttempt(Entity<TutorialInteractSoftLockComponent> ent, ref InteractionAttemptEvent args)
    {
        if (args.Cancelled || args.Target is not { } target)
            return;

        if (!ShouldBlockInteract(ent.Comp.Targets, ent.Comp.Items, target, ent))
            return;

        args.Cancelled = true;
        ShowPopup(args.Uid, ent.Comp.Popup);
    }

    private void OnEdible(Entity<TutorialSoftLockEntityComponent> ent, ref EdibleEvent args)
    {
        if (args.Cancelled)
            return;

        foreach (var player in ent.Comp.Players)
        {
            if (player != args.User ||
                !TryComp<TutorialIngestSoftLockComponent>(player, out var softLock) ||
                !IsAllowedPrototype(ent, softLock.Targets))
            {
                continue;
            }

            args.Cancelled = true;
            ShowPopup(args.User, softLock.Popup);
            return;
        }
    }

    private void OnBuiOpen(Entity<TutorialSoftLockEntityComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryComp<TutorialOpenUiSoftLockComponent>(args.User, out var softLock))
            return;

        if (!ShouldBlockInteract(softLock.Targets, null, ent, args.User))
            return;

        args.Cancel();

        if (args.Silent)
            return;

        args.Silent = true;
        ShowPopup(args.User, softLock.Popup);
    }

    private void OnOpenUiShouldMarkEntity(
        Entity<TutorialOpenUiSoftLockComponent> ent,
        ref TutorialShouldMarkEntityEvent args)
    {
        if (args.ShouldMark)
            return;

        if (!HasComp<ActivatableUIComponent>(args.Target))
            return;

        args.ShouldMark = IsAllowedPrototype(args.Target, ent.Comp.Targets);
    }

    private void OnIngestShouldMarkEntity(
        Entity<TutorialIngestSoftLockComponent> ent,
        ref TutorialShouldMarkEntityEvent args)
    {
        if (args.ShouldMark)
            return;

        args.ShouldMark = IsAllowedPrototype(args.Target, ent.Comp.Targets);
    }

    private bool TryBlockAttack(Entity<TutorialAttackSoftLockComponent> ent, AttackAttemptEvent args)
    {
        if (CanAttack(ent, args))
            return false;

        args.Cancel();
        if (!ent.Comp.Silent)
            ShowPopup(ent, ent.Comp.Popup);
        return true;
    }

    private bool TryBlockShot(Entity<TutorialAttackSoftLockComponent> ent, ref ShotAttemptedEvent args)
    {
        if (CanShoot(ent, args))
            return false;

        args.Cancel();
        if (!ent.Comp.Silent)
            ShowPopup(ent, ent.Comp.Popup);
        return true;
    }

    private bool CanAttack(Entity<TutorialAttackSoftLockComponent> ent, AttackAttemptEvent args)
    {
        // Широкий удар сначала проходит общую проверку без цели.
        // Конкретные сущности в зоне удара проверяются повторно перед нанесением урона.
        if (args.Target == null)
        {
            if (args.Weapon is { } untargetedWeapon)
                return CanUseMeleeWeapon(ent, untargetedWeapon);

            // Перед выстрелом оружейная система выполняет общую проверку без цели и оружия.
            return ent.Comp.AllowedRangedWeapons.Count > 0;
        }

        if (!IsAttackAllowedPrototype(args.Target.Value, ent.Comp.AllowedTargets))
            return false;

        if (args.Disarm)
            return ent.Comp.AllowDisarm;

        if (args.Weapon is not { } weapon)
            return false;

        return CanUseMeleeWeapon(ent, weapon);
    }

    private bool CanUseMeleeWeapon(
        Entity<TutorialAttackSoftLockComponent> ent,
        EntityUid weapon)
    {
        if (weapon == ent.Owner)
        {
            // Клиент проверяет возможность атаки без цели до определения конкретного действия.
            // Пустая рука должна пройти эту проверку как для обычного удара, так и для обезоруживания.
            return ent.Comp.AllowUnarmed || ent.Comp.AllowDisarm;
        }

        return IsAttackAllowedPrototype(weapon, ent.Comp.AllowedMeleeWeapons);
    }

    private bool CanShoot(Entity<TutorialAttackSoftLockComponent> ent, ShotAttemptedEvent args)
    {
        return IsAttackAllowedPrototype(args.Used, ent.Comp.AllowedRangedWeapons);
    }

    private bool IsAttackAllowedPrototype(EntityUid uid, List<EntProtoId> allowedPrototypes)
    {
        return allowedPrototypes.Count > 0 && HasBlockedPrototype(uid, allowedPrototypes);
    }

    private bool ShouldBlockInteract(
        List<EntProtoId>? targets,
        List<EntProtoId>? items,
        EntityUid entity,
        EntityUid item)
    {
        if (items?.Count == 0 && targets?.Count == 0)
            return false;

        if (items?.Count > 0 && !HasBlockedPrototype(item, items))
            return false;

        if (targets?.Count > 0 && !HasBlockedPrototype(entity, targets))
            return false;

        return true;
    }
}
