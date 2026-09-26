using System.Linq;
using Content.Server._Sunrise.BloodCult.UI;
using Content.Server.Body.Components;
using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds;
using Content.Server.Destructible.Thresholds.Behaviors;
using Content.Shared._Sunrise.BloodCult;
using Content.Shared._Sunrise.BloodCult.Actions;
using Content.Shared._Sunrise.BloodCult.Components;
using Content.Shared._Sunrise.BloodCult.Items;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Body.Components;
using Content.Shared.Cuffs.Components;
using Content.Shared.Destructible;
using Content.Shared.Destructible.Thresholds;
using Content.Shared.Destructible.Thresholds.Triggers;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Stacks;
using Content.Shared.StatusEffect;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.BloodCult.Runes.Systems
{
    public partial class BloodCultSystem
    {
        private void InitializeActions()
        {
            SubscribeLocalEvent<BloodCultistComponent, CultTwistedConstructionActionEvent>(OnTwistedConstructionAction);
            SubscribeLocalEvent<BloodCultistComponent, CultSummonDaggerActionEvent>(OnSummonDaggerAction);
            SubscribeLocalEvent<BloodCultistComponent, CultEmpPulseTargetActionEvent>(OnElectromagneticPulse);
            SubscribeLocalEvent<BloodCultistComponent, CultConcealPresenceWorldActionEvent>(OnConcealPresence);
            SubscribeLocalEvent<BloodCultistComponent, CultTeleportTargetActionEvent>(OnTeleport);
            SubscribeLocalEvent<BloodCultistComponent, CultStunTargetActionEvent>(OnStunTarget);
            SubscribeLocalEvent<BloodCultistComponent, CultShadowShacklesTargetActionEvent>(OnShadowShackles);
            SubscribeLocalEvent<BloodCultistComponent, ShadowShacklesDoAfterEvent>(OnShadowShacklesDoAfter);
            SubscribeLocalEvent<BloodCultistComponent, TeleportSpellUsedEvent>(OnTeleportSpellUser);
            SubscribeLocalEvent<BloodCultistComponent, TwistedConstructSpellUsedEvent>(OnTwistedConstructSpellUser);
            SubscribeLocalEvent<BloodCultistComponent, CultBloodRitualInstantActionEvent>(OnCultBloodRitual);
            SubscribeLocalEvent<BloodCultistComponent, CultBloodMagicInstantActionEvent>(OnCultBloodMagic);
            SubscribeLocalEvent<BloodCultistComponent, CultMagicBloodCallEvent>(OnCultMagicBlood);
            SubscribeLocalEvent<BloodCultistComponent, CultConvertAirlockEvent>(OnCultConvertAirlock);
            SubscribeLocalEvent<BloodCultistComponent, CultSpellProviderSelectedBuiMessage>(OnCultMagicBloodSelected);
            SubscribeLocalEvent<BloodCultistComponent, CultSummonCombatEquipmentTargetActionEvent>(
                OnSummonCombatEquipment);
        }

        private void OnCultMagicBloodSelected(EntityUid uid,
            BloodCultistComponent component,
            CultSpellProviderSelectedBuiMessage args)
        {
            if (!TryComp<BloodCultistComponent>(args.Actor, out var comp) ||
                !TryComp<ActionsComponent>(args.Actor, out var actionsComponent))
                return;

            var cultistsActions = 0;

            var action = BloodCultistComponent.CultistActions.FirstOrDefault(x => x.Equals(args.ActionType));

            var duplicated = false;
            foreach (var userAction in actionsComponent.Actions)
            {
                var entityPrototypeId = MetaData(userAction).EntityPrototype?.ID;
                if (entityPrototypeId != null && BloodCultistComponent.CultistActions.Contains(entityPrototypeId))
                    cultistsActions++;

                if (entityPrototypeId == action)
                    duplicated = true;
            }

            if (action == null)
                return;

            if (duplicated)
            {
                _popupSystem.PopupEntity(Loc.GetString("cult-duplicated-empowers"), uid);
                return;
            }

            var maxAllowedActions = 1;
            var timeToGetSpell = 10;
            var bloodTake = 20;

            var xform = Transform(uid);

            if (CheckNearbyEmpowerRune(xform.Coordinates))
            {
                maxAllowedActions = 4;
                timeToGetSpell = 4;
                bloodTake = 8;
            }

            if (cultistsActions >= maxAllowedActions)
            {
                _popupSystem.PopupEntity(Loc.GetString("cult-too-much-empowers"), uid);
                return;
            }

            var ev = new CultMagicBloodCallEvent
            {
                ActionId = action,
                BloodTake = bloodTake
            };

            var argsDoAfterEvent = new DoAfterArgs(_entityManager, args.Actor, timeToGetSpell, ev, args.Actor)
            {
                BreakOnMove = true,
                NeedHand = true,
                Hidden = true,
            };

            _doAfterSystem.TryStartDoAfter(argsDoAfterEvent);
        }

        private void OnCultMagicBlood(EntityUid uid, BloodCultistComponent comp, CultMagicBloodCallEvent args)
        {
            if (args.Cancelled)
                return;

            var howMuchBloodTake = -args.BloodTake;
            var action = args.ActionId;
            var user = args.User;

            if (HasComp<CultBuffComponent>(user))
                howMuchBloodTake /= 2;

            if (!TryComp<BloodstreamComponent>(user, out var bloodstreamComponent))
                return;

            _bloodstreamSystem.TryModifyBloodLevel((user, bloodstreamComponent), howMuchBloodTake);
            // SUNRISE-TODO: Допустим другие не должны слышать данный звук так как дуафтер скрыт.
            _audio.PlayLocal(new SoundPathSpecifier("/Audio/_Sunrise/BloodCult/blood.ogg"),
                user,
                user,
                AudioParams.Default.WithMaxDistance(2f));

            EntityUid? actionId = null;
            _actionsSystem.AddAction(user, ref actionId, action);
        }

        private bool CheckNearbyEmpowerRune(EntityCoordinates coordinates)
        {
            var radius = 1.0f;

            foreach (var lookupUid in _lookup.GetEntitiesInRange(coordinates, radius))
            {
                if (HasComp<CultEmpowerComponent>(lookupUid))
                    return true;
            }

            return false;
        }

        private void OnCultBloodMagic(EntityUid uid,
            BloodCultistComponent component,
            CultBloodMagicInstantActionEvent args)
        {
            if (!TryComp<ActorComponent>(args.Performer, out var actor))
                return;

            _ui.TryToggleUi(uid, CultSpellProviderUiKey.Key, actor.PlayerSession);
        }

        private void OnCultBloodRitual(EntityUid uid,
            BloodCultistComponent component,
            CultBloodRitualInstantActionEvent args)
        {
            foreach (var hand in _handsSystem.EnumerateHands(uid))
            {
                var containedEntity = _handsSystem.GetHeldItem(uid, hand);
                if (!TryComp(containedEntity, out MetaDataComponent? metaData))
                    continue;
                if (metaData.EntityPrototype == null)
                    continue;
                if (metaData.EntityPrototype.ID == CultBloodSpeelPrototypeId)
                {
                    QueueDel(containedEntity);
                    return;
                }

                _handsSystem.TryDrop(uid, checkActionBlocker: false);
            }

            var spell = Spawn(CultBloodSpeelPrototypeId, Transform(uid).Coordinates);
            var isPickup = _handsSystem.TryPickup(uid,
                spell,
                checkActionBlocker: false,
                animateUser: false,
                animate: false);
            if (!isPickup)
                QueueDel(spell);
        }

        private void OnTeleportSpellUser(EntityUid uid, BloodCultistComponent component, TeleportSpellUsedEvent args)
        {
            if (!_entityManager.TryGetComponent<ActionsComponent>(uid, out var actionsComponent))
                return;
            foreach (var userAction in actionsComponent.Actions)
            {
                // SUNRISE-TODO: Чет говно какое-то, надо переделать.
                var entityPrototypeId = MetaData(userAction).EntityPrototype?.ID;
                if (entityPrototypeId == TeleportActionPrototypeId.Id)
                    _actionsSystem.RemoveAction(uid, userAction);
            }
        }

        private void OnTwistedConstructSpellUser(EntityUid uid, BloodCultistComponent component, TwistedConstructSpellUsedEvent args)
        {
            if (!_entityManager.TryGetComponent<ActionsComponent>(uid, out var actionsComponent))
                return;
            foreach (var userAction in actionsComponent.Actions)
            {
                // SUNRISE-TODO: Чет говно какое-то, надо переделать.
                var entityPrototypeId = MetaData(userAction).EntityPrototype?.ID;
                if (entityPrototypeId == TwistedConstructionActionPrototypeId.Id)
                    _actionsSystem.RemoveAction((uid, null), userAction);
            }
        }

        private void OnShadowShacklesDoAfter(EntityUid uid,
            BloodCultistComponent component,
            ShadowShacklesDoAfterEvent args)
        {
            if (args.Handled || args.Cancelled)
                return;

            if (args.Args.Target == null)
                return;

            var cuffs = Spawn(CuffsPrototypeId, Transform(uid).Coordinates);
            if (TryComp<HandcuffComponent>(cuffs, out var handcuffComponent))
            {
                _audio.PlayPvs(handcuffComponent.EndCuffSound, cuffs);
                _cuffable.TryCuffingNow(args.Args.User, args.Args.Target.Value, cuffs);
            }
        }

        private void OnStunTarget(EntityUid uid, BloodCultistComponent component, CultStunTargetActionEvent args)
        {
            if (!HasComp<StatusEffectsComponent>(args.Target))
                return;

            if (HasComp<BorgChassisComponent>(args.Target))
                _empSystem.EmpPulse(_transformSystem.GetMapCoordinates(args.Target), 2, 100000, TimeSpan.FromSeconds(5));

            _stunSystem.TryAddParalyzeDuration(args.Target, TimeSpan.FromSeconds(3));
            _stuttering.DoStutter(args.Target, TimeSpan.FromSeconds(30), true);
            _flashSystem.Flash(args.Target, uid, null, TimeSpan.FromSeconds(3), 10);

            if (TryComp<BloodstreamComponent>(uid, out var bloodstreamComponent))
                _bloodstreamSystem.TryModifyBloodLevel((uid, bloodstreamComponent), -10);

            args.Handled = true;
        }

        private void OnTeleport(EntityUid uid, BloodCultistComponent component, CultTeleportTargetActionEvent args)
        {
            if (!TryComp<BloodstreamComponent>(args.Performer, out _) || !TryComp<ActorComponent>(uid, out var actor))
                return;

            var eui = new TeleportSpellEui(args.Performer, args.Target);
            _euiManager.OpenEui(eui, actor.PlayerSession);
            eui.StateDirty();

            args.Handled = true;
        }

        private void OnConcealPresence(EntityUid uid,
            BloodCultistComponent component,
            CultConcealPresenceWorldActionEvent args)
        {
            if (!TryComp<BloodstreamComponent>(args.Performer, out _))
                return;

            // SUNRISE-TODO: А где?
        }

        private void OnSummonCombatEquipment(
            EntityUid uid,
            BloodCultistComponent component,
            CultSummonCombatEquipmentTargetActionEvent args)
        {
            if (!TryComp<BloodstreamComponent>(args.Performer, out _))
                return;

            if (component.CultType == null ||
                !ProtoMan.TryIndex<BloodCultPrototype>($"{component.CultType.Value.ToString()}Cult", out var cultPrototype))
                return;

            _bloodstreamSystem.TryModifyBloodLevel(uid, -20);

            var coordinates = Transform(uid).Coordinates;
            var helmet = Spawn(HelmetPrototypeId, coordinates);
            var armor = Spawn(ArmorPrototypeId, coordinates);
            var shoes = Spawn(ShoesPrototypeId, coordinates);
            var bola = Spawn(BolaPrototypeId, coordinates);
            var blade = Spawn(cultPrototype.BladeProto, coordinates);

            _inventorySystem.TryUnequip(args.Target, "head");
            _inventorySystem.TryUnequip(args.Target, "outerClothing");
            _inventorySystem.TryUnequip(args.Target, "shoes");

            _inventorySystem.TryEquip(args.Target, helmet, "head", force: true);
            _inventorySystem.TryEquip(args.Target, armor, "outerClothing", force: true);
            _inventorySystem.TryEquip(args.Target, shoes, "shoes", force: true);

            _handsSystem.PickupOrDrop(args.Target, blade);
            _handsSystem.PickupOrDrop(args.Target, bola);

            args.Handled = true;
        }

        private void OnElectromagneticPulse(EntityUid uid,
            BloodCultistComponent component,
            CultEmpPulseTargetActionEvent args)
        {
            _empSystem.EmpPulse(_transformSystem.GetMapCoordinates(uid), 5, 10000, TimeSpan.FromSeconds(5));
            _empSystem.EmpPulse(_transformSystem.GetMapCoordinates(uid), 2, 100000, TimeSpan.FromSeconds(10));

            args.Handled = true;
        }

        private void OnShadowShackles(EntityUid uid,
            BloodCultistComponent component,
            CultShadowShacklesTargetActionEvent args)
        {
            if (!TryComp<BloodstreamComponent>(args.Performer, out _))
                return;

            if (!_statusEffectsSystem.HasStatusEffect(args.Target, "Stun"))
            {
                _popupSystem.PopupEntity("Цель не оглушена", uid, uid);
                return;
            }

            _doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager,
                uid,
                1f,
                new ShadowShacklesDoAfterEvent(),
                uid,
                target: args.Target,
                used: uid)
            {
                BreakOnMove = true,
                BreakOnDamage = true
            });

            args.Handled = true;
        }

        private void OnTwistedConstructionAction(
            EntityUid uid,
            BloodCultistComponent component,
            CultTwistedConstructionActionEvent args)
        {
            if (args.Handled)
                return;

            if (!TryComp<BloodstreamComponent>(args.Performer, out var bloodstreamComponent))
                return;

            if (_entityManager.TryGetComponent<StackComponent>(args.Target, out var stack))
            {
                if (stack.StackTypeId == PlasteelStackPrototypeId)
                {
                    var transform = Transform(args.Target).Coordinates;
                    var count = stack.Count;

                    _entityManager.DeleteEntity(args.Target);

                    var material = _entityManager.SpawnEntity(RunicMetalPrototypeId, transform);

                    _bloodstreamSystem.TryModifyBloodLevel((args.Performer, bloodstreamComponent), -15);

                    if (!_entityManager.TryGetComponent<StackComponent>(material, out var stackNew))
                        return;

                    _stack.SetCount((material, stackNew), count);

                    _popupSystem.PopupEntity(Loc.GetString($"Пласталь превращается в {MetaData(material).EntityName}!"),
                        args.Performer,
                        args.Performer);
                    var ev = new TwistedConstructSpellUsedEvent();
                    RaiseLocalEvent(args.Performer, ev);
                    args.Handled = true;
                }
                else if (stack.StackTypeId == SteelStackPrototypeId)
                {
                    var transform = Transform(args.Target).Coordinates;
                    var count = stack.Count;

                    if (count < 30)
                    {
                        _popupSystem.PopupEntity(Loc.GetString("Недостаточно стали"),
                            args.Performer,
                            args.Performer);
                        args.Handled = true;
                        return;
                    }

                    _entityManager.DeleteEntity(args.Target);

                    var shell = _entityManager.SpawnEntity(ConstructShellPrototypeId, transform);

                    _bloodstreamSystem.TryModifyBloodLevel((args.Performer, bloodstreamComponent), -15);

                    _popupSystem.PopupEntity(Loc.GetString($"Сталь превращается в {MetaData(shell).EntityName}!"),
                        args.Performer,
                        args.Performer);
                    var ev = new TwistedConstructSpellUsedEvent();
                    RaiseLocalEvent(args.Performer, ev);
                    args.Handled = true;
                }
            }
            else if (HasComp<AirlockComponent>(args.Target))
            {
                var ev = new CultConvertAirlockEvent();

                var argsDoAfterEvent = new DoAfterArgs(_entityManager, args.Performer, 5, ev, args.Performer, args.Target)
                {
                    BreakOnMove = true,
                    NeedHand = true,
                };

                _doAfterSystem.TryStartDoAfter(argsDoAfterEvent);
                args.Handled = true;
            }
        }

        private void OnCultConvertAirlock(EntityUid uid, BloodCultistComponent comp, CultConvertAirlockEvent args)
        {
            if (args.Cancelled || args.Target == null)
                return;

            if (!TryComp<BloodstreamComponent>(args.User, out var bloodstreamComponent))
                return;

            var transform = Transform(args.Target.Value);

            var entityStructureId = string.Empty;
            var metadata = MetaData(args.Target.Value);
            if (metadata.EntityPrototype != null)
                entityStructureId = metadata.EntityPrototype.ID;

            var airlock = _entityManager.SpawnEntity(AirlockGlassCultPrototypeId,
                transform.Coordinates);
            _entityManager.SpawnEntity(AirlockConvertEffect, transform.Coordinates);
            var xform = Transform(airlock);
            _transformSystem.SetLocalPositionRotation(airlock, transform.LocalPosition, transform.LocalRotation, xform);
            _entityManager.DeleteEntity(args.Target.Value);
            if (TryComp<DestructibleComponent>(airlock, out var destructible))
            {
                var damageThreshold = new DamageThreshold
                {
                    Trigger = new DamageTrigger { Damage = 150 }
                };
                damageThreshold.AddBehavior(new SpawnEntitiesBehavior
                {
                    Spawn = new Dictionary<EntProtoId, MinMax> { { entityStructureId, new MinMax{Min = 1, Max = 1} } },
                    Offset = 0f
                });
                damageThreshold.AddBehavior(new DoActsBehavior
                {
                    Acts = ThresholdActs.Destruction
                });
                _destructible.ReplaceThresholds((airlock, destructible), damageThreshold);
            }

            _bloodstreamSystem.TryModifyBloodLevel((args.User, bloodstreamComponent), -15);

            var ev = new TwistedConstructSpellUsedEvent();
            RaiseLocalEvent(args.User, ev);
        }

        private void OnSummonDaggerAction(EntityUid uid,
            BloodCultistComponent component,
            CultSummonDaggerActionEvent args)
        {
            if (args.Handled)
                return;

            if (!TryComp<BloodstreamComponent>(args.Performer, out var bloodstreamComponent))
                return;

            if (component.CultType == null ||
                !ProtoMan.TryIndex<BloodCultPrototype>($"{component.CultType.Value.ToString()}Cult", out var cultPrototype))
                return;

            var xform = Transform(args.Performer).Coordinates;
            var dagger = _entityManager.SpawnEntity(cultPrototype.DaggerProto, xform);

            _bloodstreamSystem.TryModifyBloodLevel((args.Performer, bloodstreamComponent), -30);
            _handsSystem.TryPickupAnyHand(args.Performer, dagger);
            args.Handled = true;
        }
    }
}
