using System.Linq;
using Content.Server._Sunrise.BloodCult.Items.Components;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.Hands.Systems;
using Content.Server.Popups;
using Content.Shared._Sunrise.BloodCult.Components;
using Content.Shared._Sunrise.BloodCult.Items;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Timing;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Collections;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.BloodCult.Items.Systems;

public sealed partial class CultBloodSpellSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private BloodstreamSystem _bloodstreamSystem = default!;
    [Dependency] private DamageableSystem _damageableSystem = default!;
    [Dependency] private HandsSystem _handsSystem = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private PopupSystem _popupSystem = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionSystem = default!;
    [Dependency] private TransformSystem _transformSystem = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CultBloodSpellComponent, GotUnequippedHandEvent>(OnGotUnequippedHand);
        SubscribeLocalEvent<BloodBoltBarrageComponent, GotUnequippedHandEvent>(OnGotUnequippedHand);
        SubscribeLocalEvent<BloodBoltBarrageComponent, ShotAttemptedEvent>(BarrageShotAttempt);
        SubscribeLocalEvent<BloodBoltBarrageComponent, GunShotEvent>(BarrageShot);
        SubscribeLocalEvent<CultBloodSpellComponent, AfterInteractEvent>(OnInteractEvent);
        SubscribeLocalEvent<CultBloodSpellComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<CultBloodSpellComponent, CultBloodSpellCreateOrbBuiMessage>(OnRequestCreateOrb);
        SubscribeLocalEvent<CultBloodSpellComponent, CountSelectorMessage>(OnCreateOrb);
        SubscribeLocalEvent<CultBloodSpellComponent, CultBloodSpellCreateBloodSpearBuiMessage>(
            OnRequestCreateBloodSpear);
        SubscribeLocalEvent<CultBloodSpellComponent, CultBloodSpellCreateBloodBoltBarrageBuiMessage>(
            OnRequestCreateBloodBoltBarrage);
        SubscribeLocalEvent<CultBloodSpellComponent, ExaminedEvent>(OnExamine);
    }

    private void BarrageShotAttempt(Entity<BloodBoltBarrageComponent> entity, ref ShotAttemptedEvent args)
    {
        if (!TryComp<HandsComponent>(args.User, out var handsComponent))
            return;

        var currentHand = _handsSystem.GetActiveHand((args.User, handsComponent));

        if (currentHand == null)
            return;

        var enumerateHands = _handsSystem.EnumerateHands((args.User, handsComponent));

        var otherHand = enumerateHands.FirstOrDefault(hand => hand != currentHand);

        if (otherHand == null || !_handsSystem.CanPickupToHand(args.User, entity.Owner, otherHand))
        {
            _popupSystem.PopupEntity($"Рука занята",
                args.User,
                args.User,
                PopupType.Large);
            args.Cancel();
        }
    }

    private void BarrageShot(Entity<BloodBoltBarrageComponent> entity, ref GunShotEvent args)
    {
        if (!TryComp<HandsComponent>(args.User, out var handsComponent))
            return;

        var currentHand = _handsSystem.GetActiveHand(args.User);

        if (currentHand == null)
            return;

        var otherHand = handsComponent.Hands.FirstOrDefault(h => h.Key != currentHand);

        _handsSystem.TryPickup(args.User, entity.Owner, otherHand.Key, checkActionBlocker: false);

        _handsSystem.SetActiveHand(args.User, otherHand.Key);
    }

    private void OnExamine(EntityUid uid, CultBloodSpellComponent component, ExaminedEvent args)
    {
        if (!TryComp<BloodCultistComponent>(args.Examiner, out var cultistComponent))
            return;

        args.PushMarkup($"[bold][color=white]Доступно {cultistComponent.BloodCharges} зарядов[/color][bold]");
    }

    private void OnCreateOrb(EntityUid uid, CultBloodSpellComponent component, CountSelectorMessage args)
    {
        if (!TryComp<BloodCultistComponent>(args.Actor, out var comp))
            return;

        var count = Math.Max(component.BloodOrbMinCost, args.Count);

        if (comp.BloodCharges < count)
            return;

        var orb = Spawn(component.BlodOrbSpawnId, _transformSystem.GetMapCoordinates(uid));
        var bloodOrb = EnsureComp<CultBloodOrbComponent>(orb);
        bloodOrb.BloodCharges = args.Count;
        comp.BloodCharges -= args.Count; // По идее теперь забирает столько сколько потрачено на создание
    }

    private void OnRequestCreateOrb(EntityUid uid,
        CultBloodSpellComponent component,
        CultBloodSpellCreateOrbBuiMessage args)
    {
        if (!TryComp<BloodCultistComponent>(args.Actor, out var comp))
            return;

        _ui.OpenUi(uid, CountSelectorUIKey.Key, args.Actor);
    }

    private void OnRequestCreateBloodSpear(EntityUid uid,
        CultBloodSpellComponent component,
        CultBloodSpellCreateBloodSpearBuiMessage args)
    {
        if (!TryComp<BloodCultistComponent>(args.Actor, out var comp))
            return;

        if (comp.BloodCharges < component.BloodSpearCost)
            return;

        var bloodSpear = Spawn(component.BloodSpearSpawnId, _transformSystem.GetMapCoordinates(uid));
        var bloodSpearOwner = EnsureComp<BloodSpearOwnerComponent>(args.Actor);
        bloodSpearOwner.Spear = bloodSpear;
        var bloodSpearComp = EnsureComp<CultBloodSpearComponent>(bloodSpear);
        bloodSpearComp.SpearOwner = args.Actor;
        _handsSystem.TryDrop(args.Actor, uid, checkActionBlocker: false);
        _handsSystem.TryPickup(args.Actor, bloodSpear, checkActionBlocker: false);
        comp.BloodCharges -= component.BloodSpearCost;
    }

    private void OnRequestCreateBloodBoltBarrage(EntityUid uid,
        CultBloodSpellComponent component,
        CultBloodSpellCreateBloodBoltBarrageBuiMessage args)
    {
        if (!TryComp<BloodCultistComponent>(args.Actor, out var comp))
            return;

        if (comp.BloodCharges < component.BloodBoltBarrageCost)
            return;

        var bloodBoltBarrage = Spawn(component.BloodBoltBarrageSpawnId, _transformSystem.GetMapCoordinates(uid));
        _handsSystem.TryDrop(args.Actor, uid, checkActionBlocker: false);
        _handsSystem.TryPickup(args.Actor, bloodBoltBarrage, checkActionBlocker: false);
        comp.BloodCharges -= component.BloodBoltBarrageCost;
    }

    private void OnUseInHand(EntityUid uid, CultBloodSpellComponent component, UseInHandEvent args)
    {
        if (!TryComp<BloodCultistComponent>(args.User, out _) || !TryComp<ActorComponent>(args.User, out var actor))
            return;

        _ui.OpenUi(uid, CultBloodSpellUiKey.Key, actor.PlayerSession);
    }

    private void OnInteractEvent(EntityUid uid, CultBloodSpellComponent component, AfterInteractEvent args)
    {
        if (!args.CanReach || args.Handled)
            return;

        if (!TryComp(uid, out UseDelayComponent? useDelay) || _useDelay.IsDelayed((uid, useDelay)))
            return;

        if (!TryComp<BloodCultistComponent>(args.User, out var cultistComponent))
            return;

        if (TryComp<CultBloodOrbComponent>(args.Target, out var bloodOrbComponent))
        {
            cultistComponent.BloodCharges += bloodOrbComponent.BloodCharges;
            QueueDel(args.Target);
            _popupSystem.PopupEntity($"Собрано {bloodOrbComponent.BloodCharges} зарядов",
                args.User,
                args.User,
                PopupType.Large);
            _audioSystem.PlayPvs(component.BloodAbsorbSound, args.User, component.BloodAbsorbSound.Params);
            args.Handled = true;
            return;
        }

        if (HasComp<BloodCultistComponent>(args.Target) || HasComp<ConstructComponent>(args.Target))
        {
            args.Handled = HealCultist(args.Target.Value, args.User, cultistComponent, component);
            return;
        }

        if (TryComp<BloodstreamComponent>(args.Target, out var bloodstreamComponent))
        {
            if (_solutionSystem.ResolveSolution(args.Target.Value,
                    bloodstreamComponent.BloodSolutionName,
                    ref bloodstreamComponent.BloodSolution,
                    out var bloodSolution))
            {
                if (bloodSolution.Volume > 250)
                {
                    var blood = bloodSolution.SplitSolutionWithOnly(
                        100,
                        "Blood");
                    cultistComponent.BloodCharges += blood.Volume / 2;
                    _popupSystem.PopupEntity($"Собрано {blood.Volume / 2} зарядов",
                        args.User,
                        args.User,
                        PopupType.Large);
                    _audioSystem.PlayPvs(component.BloodAbsorbSound, args.User, component.BloodAbsorbSound.Params);
                    blood.RemoveAllSolution();
                }
            }

            args.Handled = true;
            return;
        }

        var getCharges = AbsorbBloodPools(args.User, args.ClickLocation, component);
        if (getCharges <= 0)
            return;

        cultistComponent.BloodCharges += getCharges;
        args.Handled = true;
    }

    private FixedPoint2 AbsorbBloodPools(EntityUid user,
        EntityCoordinates coordinates,
        CultBloodSpellComponent bloodSpell)
    {
        var puddles = new ValueList<(EntityUid Entity, string Solution)>();
        puddles.Clear();
        foreach (var entity in _lookup.GetEntitiesInRange(coordinates, bloodSpell.RadiusAbsorbBloodPools))
        {
            if (TryComp<PuddleComponent>(entity, out var puddle))
            {
                puddles.Add((entity, puddle.SolutionName));
            }
        }

        if (puddles.Count == 0)
        {
            return 0;
        }

        var absorbBlood = new Solution();
        foreach (var (puddle, solution) in puddles)
        {
            if (!_solutionSystem.TryGetSolution(puddle, solution, out var puddleSolution))
            {
                continue;
            }

            foreach (var puddleSolutionContent in puddleSolution.Value.Comp.Solution.Contents.ToList())
            {
                if (puddleSolutionContent.Reagent.Prototype != "Blood")
                    continue;

                var amount = _solutionSystem.RemoveReagent(
                    puddleSolution.Value,
                    puddleSolutionContent.Reagent,
                    puddleSolutionContent.Quantity);
                if (amount <= FixedPoint2.Zero)
                    continue;

                var blood = new Solution();
                blood.AddReagent(puddleSolutionContent.Reagent, amount);
                absorbBlood.AddSolution(blood, _prototypeManager);
                Spawn("CultTileSpawnEffect", Transform(puddle).Coordinates);
            }
        }

        if (absorbBlood.Volume == 0)
            return 0;

        var getCharges = absorbBlood.Volume / 1.5;
        _popupSystem.PopupEntity($"Собрано {getCharges} зарядов",
            user,
            user,
            PopupType.Large);
        _audioSystem.PlayPvs(bloodSpell.BloodAbsorbSound, user, bloodSpell.BloodAbsorbSound.Params);
        absorbBlood.RemoveAllSolution();
        return getCharges;
    }

    private bool HealCultist(EntityUid target,
        EntityUid user,
        BloodCultistComponent bloodCultistComponent,
        CultBloodSpellComponent bloodSpell)
    {
        var selfHeal = target == user;

        var availableCharges = bloodCultistComponent.BloodCharges;

        if (availableCharges <= 0)
            return false;

        var fillBlood = FixedPoint2.Zero;

        if (TryComp<BloodstreamComponent>(target, out var bloodstreamComponent))
        {
            if (_solutionSystem.ResolveSolution(target,
                    bloodstreamComponent.BloodSolutionName,
                    ref bloodstreamComponent.BloodSolution,
                    out var bloodSolution))
            {
                var lossBlood = bloodstreamComponent.BloodReferenceSolution.MaxVolume - bloodSolution.Volume;
                if (lossBlood > 50)
                {
                    fillBlood = FixedPoint2.Min(lossBlood, availableCharges / 2);
                    _bloodstreamSystem.TryModifyBloodLevel((target, bloodstreamComponent), fillBlood);
                    availableCharges -= fillBlood * 2;
                    bloodCultistComponent.BloodCharges -= fillBlood * 2;
                }
            }
        }

        var totalHeal = FixedPoint2.Zero;

        var healingDamage = new DamageSpecifier();

        if (TryComp<DamageableComponent>(target, out var damageableComponent))
        {
            var totalDamage = FixedPoint2.Zero;
            var damagePerGroup = _damageableSystem.GetDamagePerGroup((target, damageableComponent));
            var currentDamage = _damageableSystem.GetAllDamage((target, damageableComponent));

            if (selfHeal)
                availableCharges /= 1.65f;

            foreach (var (damageGroup, damage) in damagePerGroup)
            {
                if (!bloodSpell.HealingGroups.Contains(damageGroup))
                    continue;

                totalDamage += damage;
            }

            foreach (var (damageGroup, damage) in damagePerGroup)
            {
                if (availableCharges <= 0)
                    break;

                if (!bloodSpell.HealingGroups.Contains(damageGroup))
                    continue;

                var damageGroupSpecifier = _prototypeManager.Index<DamageGroupPrototype>(damageGroup);

                var totalDamageInGroup = FixedPoint2.Zero;

                foreach (var damageType in damageGroupSpecifier.DamageTypes)
                {
                    totalDamageInGroup += currentDamage.DamageDict.GetValueOrDefault(damageType);
                }

                if (totalDamageInGroup == 0 || totalDamage == 0)
                    continue;

                var proportionalHealGroup = (availableCharges * (damage / totalDamage));

                proportionalHealGroup = FixedPoint2.Min(proportionalHealGroup, availableCharges);

                availableCharges -= proportionalHealGroup;

                foreach (var damageType in damageGroupSpecifier.DamageTypes.ToList())
                {
                    var damageInType = currentDamage.DamageDict.GetValueOrDefault(damageType);

                    var proportionalHealType = (proportionalHealGroup * (damageInType / totalDamageInGroup));

                    proportionalHealType = FixedPoint2.Min(proportionalHealType, damageInType);

                    healingDamage.DamageDict.Add(damageType, -proportionalHealType);
                    totalHeal += proportionalHealType;
                }
            }
        }

        if (totalHeal == 0 && fillBlood == 0)
        {
            return false;
        }

        var usedCharges = totalHeal;
        if (selfHeal)
            usedCharges *= 1.65;

        bloodCultistComponent.BloodCharges -= usedCharges;
        _damageableSystem.TryChangeDamage(target, healingDamage, ignoreResistances: true);
        _popupSystem.PopupEntity($"Излечено {totalHeal} урона и восстановлено {fillBlood} крови",
            user,
            user,
            PopupType.Large);
        _audioSystem.PlayPvs(bloodSpell.BloodAbsorbSound, user, bloodSpell.BloodAbsorbSound.Params);
        return true;
    }

    private void OnGotUnequippedHand(EntityUid uid, CultBloodSpellComponent component, GotUnequippedHandEvent args)
    {
        var xform = Transform(uid);
        if (xform.ParentUid == args.User)
            return;

        QueueDel(uid);
    }

    private void OnGotUnequippedHand(EntityUid uid, BloodBoltBarrageComponent component, GotUnequippedHandEvent args)
    {
        var xform = Transform(uid);
        if (xform.ParentUid == args.User)
            return;

        if (TryComp<BloodCultistComponent>(args.User, out var cultistComponent) &&
            TryComp<BasicEntityAmmoProviderComponent>(uid, out var ammoProviderComponent) &&
            ammoProviderComponent.Count != null)
        {
            cultistComponent.BloodCharges += (FixedPoint2) ammoProviderComponent.Count * component.ShotCost;
        }

        QueueDel(uid);
    }
}
