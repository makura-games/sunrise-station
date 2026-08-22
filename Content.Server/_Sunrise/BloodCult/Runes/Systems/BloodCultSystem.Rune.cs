using System.Linq;
using System.Numerics;
using Content.Server._Sunrise.BloodCult.GameRule;
using Content.Server._Sunrise.BloodCult.Objectives.Components;
using Content.Server._Sunrise.BloodCult.Runes.Comps;
using Content.Server.Atmos.Components;
using Content.Server.Bible.Components;
using Content.Server.Body.Components;
using Content.Server.Chat.Systems;
using Content.Server.Chemistry.Components;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Raffles;
using Content.Server.Nutrition.Components;
using Content.Shared._Sunrise.BloodCult;
using Content.Shared._Sunrise.BloodCult.Components;
using Content.Shared._Sunrise.BloodCult.Items;
using Content.Shared._Sunrise.BloodCult.Runes;
using Content.Shared._Sunrise.BloodCult.UI;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.Chat;
using Content.Shared.Chemistry.Components;
using Content.Shared.Coordinates;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Ghost.Roles.Raffles;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Mind.Components;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Rejuvenate;
using Content.Shared.Verbs;
using Microsoft.EntityFrameworkCore;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Sunrise.BloodCult.Runes.Systems
{
    public partial class BloodCultSystem
    {
        private static readonly ProtoId<DamageTypePrototype> SlashDamageType = "Slash";
        private static readonly ProtoId<DamageGroupPrototype> BruteDamageGroup = "Brute";
        private static readonly ProtoId<DamageGroupPrototype> BurnDamageGroup = "Burn";

        public void InitializeRunes()
        {
            // Runes
            SubscribeLocalEvent<CultRuneTeleportComponent, MapInitEvent>(TeleportRuneInit);
            SubscribeLocalEvent<CultRuneBaseComponent, ActivateInWorldEvent>(OnActivate);
            SubscribeLocalEvent<CultRuneOfferingComponent, CultRuneInvokeEvent>(OnInvokeOffering);
            SubscribeLocalEvent<CultRuneBuffComponent, CultRuneInvokeEvent>(OnInvokeBuff);
            SubscribeLocalEvent<CultRuneTeleportComponent, CultRuneInvokeEvent>(OnInvokeTeleport);
            SubscribeLocalEvent<CultRuneApocalypseComponent, CultRuneInvokeEvent>(OnInvokeApocalypse);
            SubscribeLocalEvent<CultRuneReviveComponent, CultRuneInvokeEvent>(OnInvokeRevive);
            SubscribeLocalEvent<CultRuneBarrierComponent, CultRuneInvokeEvent>(OnInvokeBarrier);
            SubscribeLocalEvent<CultRuneSummoningComponent, CultRuneInvokeEvent>(OnInvokeSummoning);
            SubscribeLocalEvent<CultRuneBloodBoilComponent, CultRuneInvokeEvent>(OnInvokeBloodBoil);
            SubscribeLocalEvent<BloodCultistComponent, SummonNarsieDoAfterEvent>(NarsieSpawn);
            SubscribeLocalEvent<CultEmpowerComponent, ActivateInWorldEvent>(OnActiveInWorld);
            SubscribeLocalEvent<BloodBoilProjectileComponent, PreventCollideEvent>(BloodBoilCollide);

            // UI
            SubscribeLocalEvent<RuneDrawerProviderComponent, UseInHandEvent>(OnRuneDrawerUseInHand);
            SubscribeLocalEvent<RuneDrawerProviderComponent, GetVerbsEvent<ActivationVerb>>(
                OnRuneDrawerInteractionVerb);
            SubscribeLocalEvent<CultRuneTeleportComponent, GetVerbsEvent<ActivationVerb>>(
                OnTeleportRuneInteractionVerb);
            SubscribeLocalEvent<RuneDrawerProviderComponent, ListViewItemSelectedMessage>(OnRuneSelected);
            SubscribeLocalEvent<CultRuneTeleportComponent, TeleportRunesListWindowItemSelectedMessage>(
                OnTeleportRuneSelected);

            SubscribeLocalEvent<CultRuneSummoningProviderComponent, SummonCultistListWindowItemSelectedMessage>(
                OnCultistSelected);

            // Rune drawing/erasing
            SubscribeLocalEvent<BloodCultistComponent, CultDrawEvent>(OnDraw);
            SubscribeLocalEvent<CultRuneTeleportComponent, NameSelectorMessage>(OnChoose);
            SubscribeLocalEvent<CultRuneBaseComponent, InteractUsingEvent>(TryErase);
            SubscribeLocalEvent<CultRuneBaseComponent, CultEraseEvent>(OnErase);
            SubscribeLocalEvent<CultRuneBaseComponent, StartCollideEvent>(HandleCollision);
            SubscribeLocalEvent<CultRuneReviveComponent, ExaminedEvent>(OnExamine);
        }

        private void OnExamine(EntityUid uid, CultRuneReviveComponent component, ExaminedEvent args)
        {
            var rule = _bloodCultRuleSystem.GetRule();
            if (rule == null)
                return;

            var revivals = Math.Max(0, rule.SacrificeCount / 3);
            args.PushMarkup($"[bold][color=white] Доступно воскрешений: {revivals} [/color][bold]");
        }

        private void TeleportRuneInit(EntityUid uid, CultRuneTeleportComponent component, MapInitEvent args)
        {
            var xform = Transform(uid);
            var location = FormattedMessage.RemoveMarkupOrThrow(_navMap.GetNearestBeaconString((uid, xform), true));
            component.Label = location;
        }

        /*
         * Rune draw start ----
         */

        private void OnTeleportRuneInteractionVerb(EntityUid uid,
            CultRuneTeleportComponent component,
            GetVerbsEvent<ActivationVerb> args)
        {
            if (!TryComp<ActorComponent>(args.User, out var actorComponent))
                return;

            if (!HasComp<BloodCultistComponent>(args.User))
                return;

            args.Verbs.Add(new ActivationVerb()
            {
                Text = "Изменить название",
                Act = () =>
                {
                    _ui.OpenUi(uid, NameSelectorUIKey.Key, actorComponent.PlayerSession);
                }
            });
        }

        private void OnRuneDrawerInteractionVerb(EntityUid uid,
            RuneDrawerProviderComponent component,
            GetVerbsEvent<ActivationVerb> args)
        {
            if (!TryComp<ActorComponent>(args.User, out var actorComponent))
                return;

            if (!HasComp<BloodCultistComponent>(args.User))
                return;

            args.Verbs.Add(new ActivationVerb()
            {
                Text = "Начертить руну",
                Act = () =>
                {
                    _ui.OpenUi(uid, ListViewSelectorUiKey.Key, actorComponent.PlayerSession);
                }
            });
        }

        private void OnRuneDrawerUseInHand(EntityUid uid, RuneDrawerProviderComponent component, UseInHandEvent args)
        {
            if (!TryComp<ActorComponent>(args.User, out var actorComponent))
                return;

            if (!HasComp<BloodCultistComponent>(args.User))
                return;

            _ui.OpenUi(uid, ListViewSelectorUiKey.Key, actorComponent.PlayerSession);
        }

        private void OnRuneSelected(EntityUid uid,
            RuneDrawerProviderComponent component,
            ListViewItemSelectedMessage args)
        {
            var runePrototype = args.SelectedItem;

            if (!IsCorrectLocation(args.Actor, out var coords))
                return;

            if (!TryComp<ActorComponent>(args.Actor, out var actorComponent))
                return;

            if (!TryDraw(args.Actor, runePrototype))
                return;
        }

        private bool TryDraw(EntityUid whoCalled, string runePrototype)
        {
            _timeToDraw = 4f;

            if (HasComp<CultBuffComponent>(whoCalled))
                _timeToDraw /= 2;

            if (runePrototype == ApocalypseRunePrototypeId)
            {
                var rule = _bloodCultRuleSystem.GetRule();
                if (rule == null)
                    return false;

                var targetsKill = _bloodCultRuleSystem.TargetsKill();

                if (!targetsKill)
                {
                    _popupSystem.PopupEntity("Цели не были принесены в жертву.", whoCalled, whoCalled);
                    return false;
                }

                _timeToDraw = 45.0f;
                var xform = Transform(whoCalled);
                var location = FormattedMessage.RemoveMarkupOrThrow(_navMap.GetNearestBeaconString((whoCalled, xform)));
                _chat.DispatchGlobalAnnouncement(
                    Loc.GetString("cult-started-drawing-rune-end", ("location", location)),
                    Loc.GetString("centcomm-cult-alert"),
                    true,
                    _apocRuneStartDrawing,
                    colorOverride: Color.Red);
            }

             if (!IsAllowedToDraw(whoCalled))
                return false;

            var ev = new CultDrawEvent
            {
                Rune = runePrototype
            };

            var argsDoAfterEvent = new DoAfterArgs(_entityManager, whoCalled, _timeToDraw, ev, whoCalled)
            {
                BreakOnMove = true,
                NeedHand = true
            };

            if (!_doAfterSystem.TryStartDoAfter(argsDoAfterEvent))
                return false;

            _audio.PlayPvs("/Audio/_Sunrise/BloodCult/butcher.ogg", whoCalled, AudioParams.Default.WithMaxDistance(2f));
            return true;
        }

        private void OnDraw(EntityUid uid, BloodCultistComponent comp, CultDrawEvent args)
        {
            if (args.Cancelled)
                return;

            var howMuchBloodTake = -10;
            var rune = args.Rune;
            var user = args.User;

            if (HasComp<CultBuffComponent>(user))
                howMuchBloodTake /= 2;

            if (!TryComp<BloodstreamComponent>(user, out var bloodstreamComponent))
                return;

            _bloodstreamSystem.TryModifyBloodLevel((user, bloodstreamComponent), howMuchBloodTake);

            SpawnRune(user, rune);

            _audio.PlayPvs("/Audio/_Sunrise/BloodCult/blood.ogg", user, AudioParams.Default.WithMaxDistance(2f));

        }

        private void OnChoose(EntityUid uid, CultRuneTeleportComponent component, NameSelectorMessage args)
        {
            if (!TryComp<ActorComponent>(args.Actor, out var actorComponent))
                return;

            _ui.CloseUi(uid, NameSelectorUIKey.Key, actorComponent.PlayerSession);

            var label = string.IsNullOrEmpty(args.Name) ? Loc.GetString("cult-teleport-rune-default-label") : args.Name;

            if (label.Length > 18)
            {
                label = label.Substring(0, 18);
            }

            component.Label = label;
        }

        //Erasing start

        private void TryErase(EntityUid uid, CultRuneBaseComponent component, InteractUsingEvent args)
        {
            var user = args.User;
            var target = args.Target;
            var time = 3;

            if (!HasComp<RuneDrawerProviderComponent>(args.Used) && !HasComp<BibleComponent>(args.Used))
                return;

            if (!HasComp<BloodCultistComponent>(user))
                return;

            if (HasComp<CultBuffComponent>(user))
                time /= 2;

            var netEntity = GetNetEntity(target);

            var ev = new CultEraseEvent
            {
                TargetEntityId = netEntity
            };

            var argsDoAfterEvent = new DoAfterArgs(_entityManager, user, time, ev, target)
            {
                BreakOnMove = true,
                NeedHand = true
            };

            if (_doAfterSystem.TryStartDoAfter(argsDoAfterEvent))
            {
                _popupSystem.PopupEntity(Loc.GetString("cult-started-erasing-rune"), target);
            }
        }

        private void OnErase(EntityUid uid, CultRuneBaseComponent component, CultEraseEvent args)
        {
            if (args.Cancelled)
                return;

            var target = GetEntity(args.TargetEntityId);

            _entityManager.DeleteEntity(target);
            _popupSystem.PopupEntity(Loc.GetString("cult-erased-rune"), args.User);
        }

        private void HandleCollision(EntityUid uid, CultRuneBaseComponent component, ref StartCollideEvent args)
        {
            if (!HasComp<VaporComponent>(args.OtherEntity) ||
                !TryComp<SolutionComponent>(args.OtherEntity, out var vapor))
                return;

            if (vapor.Solution.Contents.Any(x => x.Reagent.Prototype == "Holywater"))
            {
                Del(uid);
            }
        }

        //Erasing end

        /*
         * Rune draw end ----
         */

        //------------------------------------------//

        /*
         * Base Start ----
         */

        private void OnActivate(EntityUid uid, CultRuneBaseComponent component, ActivateInWorldEvent args)
        {
            if (args.Handled)
                return;

            args.Handled = true;

            if (!HasComp<BloodCultistComponent>(args.User))
                return;

            var cultists = new HashSet<EntityUid>
            {
                args.User
            };

            if (component.InvokersMinCount > 1 || component.GatherInvokers)
                cultists = GatherCultists(uid, component.CultistGatheringRange);

            if (cultists.Count < component.InvokersMinCount)
            {
                _popupSystem.PopupEntity(Loc.GetString("not-enough-cultists"), args.User, args.User);
                return;
            }

            var ev = new CultRuneInvokeEvent(uid, args.User, cultists);
            RaiseLocalEvent(uid, ev);

            if (ev.Result)
            {
                OnAfterInvoke(component.InvokePhrase, cultists);
            }
        }

        private void OnAfterInvoke(string phase, HashSet<EntityUid> cultists)
        {
            foreach (var cultist in cultists)
            {
                _chat.TrySendInGameICMessage(cultist,
                    phase,
                    InGameICChatType.Speak,
                    false,
                    false,
                    null,
                    null,
                    null,
                    false);
            }
        }

        /*
         * Base End ----
         */

        //------------------------------------------//

        /*
         * Offering Rune START ----
         */

        private void OnInvokeOffering(EntityUid uid, CultRuneOfferingComponent component, CultRuneInvokeEvent args)
        {
            var rule = _bloodCultRuleSystem.GetRule();
            if (rule == null)
                return;

            var targets =
                _lookup.GetEntitiesInRange(uid, component.RangeTarget, LookupFlags.Dynamic | LookupFlags.Sundries);

            targets.RemoveWhere(x =>
                HasComp<BloodCultistComponent>(x) ||
                HasComp<ConstructComponent>(x) ||
                !HasComp<MobStateComponent>(x));

            if (targets.Count == 0)
                return;

            var victim = FindNearestTarget(uid, targets.ToList());

            if (victim == null)
                return;

            _entityManager.TryGetComponent<MobStateComponent>(victim.Value, out var state);

            if (state == null)
                return;

            bool result;

            var cultTargets = rule.CultTargets;

            var isTarget = cultTargets.ContainsKey(victim.Value);

            if (isTarget)
            {
                result = Sacrifice(uid, victim.Value, args.User, args.Cultists, rule, isTarget);
                args.Result = result;
                return;
            }

            if (state.CurrentState != MobState.Dead)
            {
                var hasMind = _mindSystem.TryGetMind(victim.Value, out var mindId, out var mind);

                var canConvert = !HasComp<MindShieldComponent>(victim.Value)
                                      && !HasComp<BibleUserComponent>(victim.Value);

                if (hasMind && canConvert )
                {
                    if (args.Cultists.Count < component.ConvertMinCount)
                    {
                        _popupSystem.PopupEntity(Loc.GetString("cult-convert-not-enough-cultists"), args.User, args.User);
                        args.Result = false;
                        return;
                    }

                    result = Convert(uid, victim.Value, args.User, args.Cultists, rule);
                }
                else
                {
                    result = Sacrifice(uid, victim.Value, args.User, args.Cultists, rule, isTarget);
                }
            }
            else
            {
                // Жертва мертва, выполняется альтернативное действие
                result = SacrificeNonObjectiveDead(uid, victim.Value, args.User, args.Cultists, rule);
            }

            args.Result = result;
        }

        private bool Sacrifice(
            EntityUid rune,
            EntityUid target,
            EntityUid user,
            HashSet<EntityUid> cultists,
            BloodCultRuleComponent rule,
            bool isTarget = false)
        {
            if (!_entityManager.TryGetComponent<CultRuneOfferingComponent>(rune, out var offering))
                return false;

            if (cultists.Count < offering.SacrificeMinCount)
            {
                _popupSystem.PopupEntity(Loc.GetString("cult-convert-not-enough-cultists"), user, user);
                return false;
            }

            if (isTarget)
            {
                rule.CultTargets[target] = true;
                var query = EntityQueryEnumerator<KillCultistTargetsConditionComponent>();

                while (query.MoveNext(out var obj, out var killCultistTargetsComponent))
                {
                    _cultistTargetsConditionSystem.RefresTitle(obj, rule.CultTargets, killCultistTargetsComponent);
                }

                _gibbing.Gib(target);
                _bloodCultRuleSystem.ChangeSacrificeCount(rule, rule.SacrificeCount + 1);

                return true;
            }

            if (!SpawnShard(target))
            {
                _gibbing.Gib(target);
            }

            _bloodCultRuleSystem.ChangeSacrificeCount(rule, rule.SacrificeCount + 1);
            return true;
        }

        private bool SacrificeNonObjectiveDead(
            EntityUid rune,
            EntityUid target,
            EntityUid user,
            HashSet<EntityUid> cultists,
            BloodCultRuleComponent rule)
        {
            if (!_entityManager.TryGetComponent<CultRuneOfferingComponent>(rune, out var offering))
                return false;

            if (cultists.Count < offering.SacrificeDeadMinCount)
            {
                _popupSystem.PopupEntity(Loc.GetString("cult-sacrifice-not-enough-cultists"), user, user);
                return false;
            }

            if (!SpawnShard(target))
            {
                _gibbing.Gib(target);
            }

            _bloodCultRuleSystem.ChangeSacrificeCount(rule, rule.SacrificeCount + 1);
            return true;
        }

        private bool Convert(EntityUid rune, EntityUid target, EntityUid user, HashSet<EntityUid> cultists,
            BloodCultRuleComponent rule)
        {
            if (!_entityManager.TryGetComponent<CultRuneOfferingComponent>(rune, out var offering))
                return false;

            if (cultists.Count < offering.ConvertMinCount)
            {
                _popupSystem.PopupEntity(Loc.GetString("cult-offering-rune-not-enough"), user, user);
                return false;
            }

            _bloodCultRuleSystem.MakeCultist(target, rule);
            _stunSystem.TryAddParalyzeDuration(target, TimeSpan.FromSeconds(2f));
            HealCultist(target);

            if (TryComp<CuffableComponent>(target, out var cuffs) && cuffs.Container.ContainedEntities.Count >= 1)
                _cuffable.Uncuff(target, cuffs.Container.ContainedEntities[^1], cuffs.Container.ContainedEntities[^1]);

            return true;
        }

        /*
         * Offering Rune END ----
         */

        //------------------------------------------//

        /*
         * Buff Rune Start ----
         */

        private void OnInvokeBuff(EntityUid uid, CultRuneBuffComponent component, CultRuneInvokeEvent args)
        {
            var targets =
                _lookup.GetEntitiesInRange(uid, component.RangeTarget, LookupFlags.Dynamic | LookupFlags.Sundries);

            targets.RemoveWhere(x =>
                !_entityManager.HasComponent<HumanoidProfileComponent>(x) ||
                !_entityManager.HasComponent<BloodCultistComponent>(x));

            if (targets.Count == 0)
                return;

            var victim = FindNearestTarget(uid, targets.ToList());

            if (victim == null)
                return;

            _entityManager.TryGetComponent<MobStateComponent>(victim.Value, out var state);

            var result = false;

            if (state != null && state.CurrentState != MobState.Dead)
            {
                result = AddCultistBuff(victim.Value, args.User);
            }

            args.Result = result;
        }

        private bool AddCultistBuff(EntityUid target, EntityUid user)
        {
            if (HasComp<CultBuffComponent>(target))
            {
                _popupSystem.PopupEntity(Loc.GetString("cult-buff-already-buffed"), user, user);
                return false;
            }

            EnsureComp<CultBuffComponent>(target);
            return true;
        }

        private void OnActiveInWorld(EntityUid uid, CultEmpowerComponent component, ActivateInWorldEvent args)
        {
            if (!TryComp<BloodCultistComponent>(args.User, out _) || !TryComp<ActorComponent>(args.User, out var actor))
                return;

            _ui.OpenUi(uid, CultSpellProviderUiKey.Key, actor.PlayerSession);
        }

        /*
         * Empower Rune End ----
         */

        //------------------------------------------//

        /*
         * Teleport rune start ----
         */

        private void OnInvokeTeleport(EntityUid uid, CultRuneTeleportComponent component, CultRuneInvokeEvent args)
        {
            args.Result = Teleport(uid, args.User);
        }

        private bool Teleport(EntityUid rune, EntityUid user)
        {
            var list = new List<int>();
            var labels = new List<string>();
            var distance = new List<string>();

            var query = EntityQueryEnumerator<CultRuneTeleportComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var comp, out var xform))
            {
                if (comp.Label == null)
                    continue;

                if (uid == rune)
                    continue;

                if (!int.TryParse(uid.ToString(), out var intValue))
                    continue;

                list.Add(intValue);
                labels.Add(comp.Label);
                distance.Add(GetDistance(rune, xform));
            }

            if (!TryComp<ActorComponent>(user, out var actorComponent))
                return false;

            if (list.Count == 0)
            {
                _popupSystem.PopupEntity(Loc.GetString("cult-teleport-rune-not-found"), user, user);
                return false;
            }

            _ui.SetUiState(rune, RuneTeleporterUiKey.Key, new TeleportRunesListWindowBUIState(list, labels, distance));

            if (_ui.IsUiOpen(rune, RuneTeleporterUiKey.Key))
                return false;

            _ui.TryToggleUi(rune, RuneTeleporterUiKey.Key, actorComponent.PlayerSession);
            return true;
        }

        private void OnTeleportRuneSelected(
            EntityUid uid,
            CultRuneTeleportComponent component,
            TeleportRunesListWindowItemSelectedMessage args)
        {
            var targets = new HashSet<EntityUid>();

            _lookup.GetEntitiesInRange(uid, component.RangeTarget, targets, LookupFlags.Dynamic | LookupFlags.Sundries);

            if (targets.Count == 0)
                return;

            var selectedRune = new EntityUid(args.SelectedItem);
            var baseRune = uid;

            if (!TryComp(selectedRune, out TransformComponent? xFormSelected) ||
                !TryComp(baseRune, out TransformComponent? xFormBase))
                return;

            foreach (var target in targets)
            {
                if (TryComp<PullableComponent>(target, out var pullable))
                    _pulling.TryStopPull(target, pullable);
                if (HasComp<HumanoidProfileComponent>(target) && TryComp(target, out TransformComponent? targetm))
                {
                    _entityManager.SpawnEntity(TeleportInEffect, xFormSelected.Coordinates);
                    _entityManager.SpawnEntity(TeleportOutEffect, targetm.Coordinates);
                }
                _xform.SetCoordinates(target, xFormSelected.Coordinates);
            }

            // Play tp sound
            _audio.PlayPvs(_teleportInSound, xFormSelected.Coordinates);
            _audio.PlayPvs(_teleportOutSound, xFormBase.Coordinates);
        }

        /*
         * Teleport rune end ----
         */

        //------------------------------------------//

        /*
         * Apocalypse rune start ----
         */

        private void OnInvokeApocalypse(EntityUid uid, CultRuneApocalypseComponent component, CultRuneInvokeEvent args)
        {
            args.Result = TrySummonNarsie(args.User, args.Cultists, component);
        }

        private bool TrySummonNarsie(EntityUid user, HashSet<EntityUid> cultists, CultRuneApocalypseComponent component)
        {
            var targetsKill = _bloodCultRuleSystem.TargetsKill();

            if (!targetsKill)
            {
                _popupSystem.PopupEntity(Loc.GetString("cult-narsie-not-completed-tasks"), user, user);
                return false;
            }

            // Ну нахуй
            // var cultAwakened = _bloodCultRuleSystem.CultAwakened();
            //
            // if (!cultAwakened)
            // {
            //     _popupSystem.PopupEntity(Loc.GetString("cult-narsie-not-awakened"), user, user);
            //     return false;
            // }

            if (cultists.Count < component.SummonMinCount)
            {
                _popupSystem.PopupEntity(Loc.GetString("cult-narsie-summon-not-enough",
                    ("count", component.SummonMinCount)), user, user);
                return false;
            }

            if (_doAfterAlreadyStarted)
            {
                _popupSystem.PopupEntity(Loc.GetString("cult-narsie-already-summoning"), user, user);
                return false;
            }

            if (!TryComp<DoAfterComponent>(user, out var doAfterComponent))
            {
                if (doAfterComponent is { AwaitedDoAfters.Count: >= 1 })
                {
                    _popupSystem.PopupEntity(Loc.GetString("cult-narsie-summon-do-after"), user, user);
                    return false;
                }
            }

            var ev = new SummonNarsieDoAfterEvent();

            var argsDoAfterEvent = new DoAfterArgs(_entityManager, user, TimeSpan.FromSeconds(60), ev, user)
            {
                BreakOnMove = true
            };

            if (!_doAfterSystem.TryStartDoAfter(argsDoAfterEvent))
                return false;

            _popupSystem.PopupEntity(Loc.GetString("cult-stay-still"), user, user, PopupType.LargeCaution);

            _doAfterAlreadyStarted = true;

            _chat.DispatchGlobalAnnouncement(Loc.GetString("cult-ritual-started"),
                Loc.GetString("centcomm-cult-alert"),
                false,
                colorOverride: Color.DarkRed);

            var stream = _audio.PlayGlobal(_narsie40Sec,
                Filter.Broadcast(),
                false,
                AudioParams.Default.WithLoop(true).WithVolume(0.15f));

            _playingStream = stream?.Entity;

            return true;
        }

        private void NarsieSpawn(EntityUid uid, BloodCultistComponent component, SummonNarsieDoAfterEvent args)
        {
            if (_playingStream != null)
                _audio.Stop(_playingStream);

            _doAfterAlreadyStarted = false;

            if (args.Cancelled)
            {
                _chat.DispatchGlobalAnnouncement(Loc.GetString("cult-ritual-prevented"),
                    Loc.GetString("centcomm-cult-alert"),
                    false,
                    colorOverride: Color.DarkRed);

                return;
            }

            var transform = CompOrNull<TransformComponent>(args.User)?.Coordinates;
            if (transform == null)
                return;

            if (component.CultType == null ||
                !_prototypeManager.TryIndex<BloodCultPrototype>($"{component.CultType.Value.ToString()}Cult", out var cultPrototype))
                return;

            _entityManager.SpawnEntity(cultPrototype.GodProto, transform.Value);

            _chat.DispatchGlobalAnnouncement(Loc.GetString("cult-narsie-summoned"),
                Loc.GetString("centcomm-cult-alert"),
                true,
                _apocRuneEndDrawing,
                colorOverride: Color.DarkRed);

            var ev = new CultNarsieSummoned();
            RaiseLocalEvent(ev);
        }

        /*
         * Apocalypse rune end ----
         */

        //------------------------------------------//

        /*
         * Revive rune start ----
         */

        private void OnInvokeRevive(EntityUid uid, CultRuneReviveComponent component, CultRuneInvokeEvent args)
        {
            var targets =
                _lookup.GetEntitiesInRange(uid, component.RangeTarget, LookupFlags.Dynamic | LookupFlags.Sundries);

            targets.RemoveWhere(x =>
                !_entityManager.HasComponent<HumanoidProfileComponent>(x) || !HasComp<BloodCultistComponent>(x));

            if (targets.Count == 0)
                return;

            var victim = FindNearestTarget(uid, targets.ToList());

            if (victim == null)
                return;

            _entityManager.TryGetComponent<MobStateComponent>(victim.Value, out var state);

            if (state == null)
                return;

            if (state.CurrentState != MobState.Dead && state.CurrentState != MobState.Critical)
            {
                _popupSystem.PopupEntity(Loc.GetString("cult-revive-rune-already-alive"), args.User, args.User);
                return;
            }

            var result = Revive(victim.Value, args.User);

            args.Result = result;


            if (!_mindSystem.TryGetMind(victim.Value, out var mindid, out var mind))
            {
                EnsureComp<GhostRoleComponent>(victim.Value, out var ghost);

                EnsureComp<GhostTakeoverAvailableComponent>(victim.Value);

                ghost.RoleName = Loc.GetString("revived-cultist-name");
                ghost.RoleDescription = Loc.GetString("revived-cultist-desc");
            }
        }

        private bool Revive(EntityUid target, EntityUid user)
        {
            var rule = _bloodCultRuleSystem.GetRule();
            if (rule == null)
                return false;

            if (rule.SacrificeCount < 3)
            {
                _popupSystem.PopupEntity(Loc.GetString("cult-revive-rune-not-enough-sacrifices"), user, user);
                return false;
            }

            _bloodCultRuleSystem.ChangeSacrificeCount(rule, rule.SacrificeCount - 3);

            _entityManager.EventBus.RaiseLocalEvent(target, new RejuvenateEvent());
            return true;
        }

        /*
         * Revive rune end ----
         */

        //------------------------------------------//

        /*
         * Barrier rune start ----
         */

        private void OnInvokeBarrier(EntityUid uid, CultRuneBarrierComponent component, CultRuneInvokeEvent args)
        {
            args.Result = SpawnBarrier(args.Rune);
        }

        private bool SpawnBarrier(EntityUid rune)
        {
            var transform = CompOrNull<TransformComponent>(rune)?.Coordinates;

            if (transform == null)
                return false;

            _entityManager.SpawnEntity(CultBarrierPrototypeId, transform.Value);
            _entityManager.DeleteEntity(rune);

            return true;
        }

        /*
         * Barrier rune end ----
         */

        //------------------------------------------//

        /*
         * Summoning rune start ----
         */

        private void OnInvokeSummoning(EntityUid uid, CultRuneSummoningComponent component, CultRuneInvokeEvent args)
        {
            args.Result = Summon(uid, args.User, args.Cultists, component);
        }

        private bool Summon(
            EntityUid rune,
            EntityUid user,
            HashSet<EntityUid> cultistHashSet,
            CultRuneSummoningComponent component)
        {

            if (!_entityManager.TryGetComponent<TransformComponent>(rune, out var runeTransform))
                return false;

            var list = new List<int>();
            var labels = new List<string>();
            var mobState = new List<string>();
            var distance = new List<string>();

            if (cultistHashSet.Count < component.SummonMinCount)
            {
                _popupSystem.PopupEntity(Loc.GetString("cult-summon-rune-need-minimum-cultists"), user, user);
                return false;
            }

            var query = EntityQueryEnumerator<BloodCultistComponent, MetaDataComponent, MobStateComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var cultist, out var meta, out var state, out var xform))
            {
                if (cultistHashSet.Contains(uid))
                    continue;

                if (!int.TryParse(uid.ToString(), out var intValue))
                    continue;

                list.Add(intValue);
                labels.Add(meta.EntityName);
                mobState.Add(GetStatus(state.CurrentState));
                distance.Add(GetDistance(rune, xform));
            }

            if (!TryComp<ActorComponent>(user, out var actorComponent))
                return false;

            if (list.Count == 0)
            {
                _popupSystem.PopupEntity(Loc.GetString("cult-cultists-not-found"), user, user);
                return false;
            }

            _entityManager.EnsureComponent<CultRuneSummoningProviderComponent>(user, out var providerComponent);
            providerComponent.BaseRune = rune;

            _ui.SetUiState(user, SummonCultistUiKey.Key, new SummonCultistListWindowBUIState(list, labels, mobState, distance));

            if (_ui.IsUiOpen(user, SummonCultistUiKey.Key))
                return false;

            _ui.TryToggleUi(user, SummonCultistUiKey.Key, actorComponent.PlayerSession);
            return true;
        }

        private void OnCultistSelected(
            EntityUid uid,
            CultRuneSummoningProviderComponent component,
            SummonCultistListWindowItemSelectedMessage args)
        {
            var target = new EntityUid(args.SelectedItem);
            var baseRune = component.BaseRune;

            if (!TryComp<PullableComponent>(target, out var pullableComponent))
                return;

            if (!TryComp<CuffableComponent>(target, out var cuffableComponent))
                return;

            if (baseRune == null)
                return;

            if (!TryComp(baseRune, out TransformComponent? xFormBase))
                return;

            var isCuffed = cuffableComponent.CuffedHandCount > 0;
            var isPulled = pullableComponent.BeingPulled;

            if (isPulled)
            {
                _popupSystem.PopupEntity("Его кто-то держит!", args.Actor);
                return;
            }

            if (isCuffed)
            {
                _popupSystem.PopupEntity("Он в наручниках!", args.Actor);
                return;
            }

            _xform.SetCoordinates(target, xFormBase.Coordinates);

            _audio.PlayPvs(_teleportInSound, xFormBase.Coordinates);

            if (HasComp<CultRuneSummoningProviderComponent>(args.Actor))
            {
                RemComp<CultRuneSummoningProviderComponent>(args.Actor);
            }

            QueueDel(baseRune);
        }

        /*
         * Summoning rune end ----
         */

        //------------------------------------------//

        /*
         * BloodBoil rune start ----
         */

        private void OnInvokeBloodBoil(EntityUid uid, CultRuneBloodBoilComponent component, CultRuneInvokeEvent args)
        {
            args.Result = PrepareShoot(uid, args.User, args.Cultists, 1.0f, component);
            if (args.Result)
            {
                QueueDel(uid);
            }
        }

        private void BloodBoilCollide(EntityUid uid, BloodBoilProjectileComponent component, ref PreventCollideEvent args)
        {
            if (HasComp<BloodCultistComponent>(args.OtherEntity) || HasComp<ConstructComponent>(args.OtherEntity))
            {
                args.Cancelled = true;
                return;
            }

            if (!HasComp<MobStateComponent>(args.OtherEntity))
            {
                args.Cancelled = true;
            }
        }

        private bool PrepareShoot(
            EntityUid rune,
            EntityUid user,
            HashSet<EntityUid> cultists,
            float severity,
            CultRuneBloodBoilComponent component)
        {
            if (cultists.Count < component.SummonMinCount)
            {
                _popupSystem.PopupEntity(Loc.GetString("cult-blood-boil-rune-need-minimum"), user, user);
                return false;
            }

            var xform = _xformQuery.GetComponent(rune);

            var projectileCount =
                (int)MathF.Round(MathHelper.Lerp(component.MinProjectiles, component.MaxProjectiles, severity));

            var inRange = _lookup.GetEntitiesInRange(rune, component.ProjectileRange * severity, LookupFlags.Dynamic);
            inRange.RemoveWhere(x =>
                !_entityManager.HasComponent<HumanoidProfileComponent>(x) ||
                _entityManager.HasComponent<BloodCultistComponent>(x) ||
                _entityManager.HasComponent<ConstructComponent>(x));

            var list = inRange.ToList();

            if (list.Count == 0)
            {
                _popupSystem.PopupEntity(Loc.GetString("cult-blood-boil-rune-no-targets"), user, user);
                return false;
            }

            foreach (var cultist in cultists)
            {
                if (HasComp<ConstructComponent>(cultist))
                    continue;

                if (!TryComp<BloodstreamComponent>(cultist, out var bloodstreamComponent))
                    return false;

                _bloodstreamSystem.TryModifyBloodLevel((cultist, bloodstreamComponent), -40);
            }

            _random.Shuffle(list);

            while (projectileCount > 0)
            {
                var target = _random.Pick(list);
                var targetCoords = _xformQuery.GetComponent(target).Coordinates.Offset(_random.NextVector2(0.5f));

                if (!_flammableQuery.TryGetComponent(target, out var fl))
                    continue;

                fl.FireStacks += _random.Next(1, 3);

                _flammableSystem.Ignite(target, target);

                Shoot(
                    rune,
                    component,
                    xform.Coordinates,
                    targetCoords,
                    severity);

                projectileCount--;
            }

            _audio.PlayPvs(_magic, rune, AudioParams.Default.WithMaxDistance(2f));

            return true;
        }

        private void Shoot(
            EntityUid uid,
            CultRuneBloodBoilComponent component,
            EntityCoordinates coords,
            EntityCoordinates targetCoords,
            float severity)
        {
            var mapPos = coords.ToMap(EntityManager, _xform);

            var spawnCoords = _mapMan.TryFindGridAt(mapPos, out var gridUid, out _)
                ? coords.WithEntityId(gridUid, EntityManager)
                : new(_mapMan.GetMapEntityId(mapPos.MapId), mapPos.Position);

            var ent = Spawn(component.ProjectilePrototype, spawnCoords);
            var direction = targetCoords.ToMapPos(EntityManager, _xform) - mapPos.Position;

            if (!TryComp<ProjectileComponent>(ent, out var comp))
                return;

            comp.Damage *= severity;

            _gunSystem.ShootProjectile(ent, direction, Vector2.Zero, uid, uid, component.ProjectileSpeed);
        }

        /*
         * BloodBoil rune end ----
         */

        //------------------------------------------//

        /*
         * Empower rune start ----
         */

        /*
         * Empower rune end ----
         */

        //------------------------------------------//

        /*
         * Helpers Start ----
         */

        private EntityUid? FindNearestTarget(EntityUid uid, List<EntityUid> targets)
        {
            if (!_entityManager.TryGetComponent<TransformComponent>(uid, out var runeTransform))
                return null;

            var range = 999f;
            EntityUid? victim = null;

            foreach (var target in targets)
            {
                if (!_entityManager.TryGetComponent<TransformComponent>(target, out var targetTransform))
                    continue;

                runeTransform.Coordinates.TryDistance(_entityManager, targetTransform.Coordinates, out var newRange);

                if (newRange < range)
                {
                    range = newRange;
                    victim = target;
                }
            }

            return victim;
        }

        private HashSet<EntityUid> GatherCultists(EntityUid uid, float range)
        {
            var entities = _lookup.GetEntitiesInRange(uid, range, LookupFlags.Dynamic);
            entities.RemoveWhere(x => !HasComp<BloodCultistComponent>(x) && !HasComp<ConstructComponent>(x));

            return entities;
        }

        private void SpawnRune(EntityUid uid, string? rune)
        {

            if (!IsCorrectLocation(uid, out var coords))
                return;

            if (rune == null)
                return;

            if (rune == ApocalypseRunePrototypeId)
            {
                // ыыыы
            }

            var damageSpecifier = new DamageSpecifier(_prototypeManager.Index(SlashDamageType), 10);
            _damageableSystem.TryChangeDamage(uid, damageSpecifier, true, false);

            _entityManager.SpawnEntity(rune, coords);
        }

        private bool SpawnShard(EntityUid target)
        {
            if (!_entityManager.TryGetComponent<MindContainerComponent>(target, out var mindComponent))
                return false;

            var transform = CompOrNull<TransformComponent>(target)?.Coordinates;

            if (transform == null)
                return false;

            var shard = _entityManager.SpawnEntity("SoulShardGhost", transform.Value);

            if (mindComponent.Mind.HasValue)
                _mindSystem.TransferTo(mindComponent.Mind.Value, shard);

            _gibbing.Gib(target);

            return true;
        }

        private bool IsAllowedToDraw(EntityUid uid)
        {
            var transform = Transform(uid);
            var gridUid = transform.GridUid;
            var tile = _turf.GetTileRef(transform.Coordinates);

            if (!gridUid.HasValue)
            {
                _popupSystem.PopupEntity(Loc.GetString("cult-cant-draw-rune"), uid, uid);
                return false;
            }

            if (!tile.HasValue)
            {
                _popupSystem.PopupEntity(Loc.GetString("cult-cant-draw-rune"), uid, uid);
                return false;
            }

            return true;
        }

        private void HealCultist(EntityUid player)
        {
            var damageSpecifier = _prototypeManager.Index(BruteDamageGroup);
            var damageSpecifier2 = _prototypeManager.Index(BurnDamageGroup);

            _damageableSystem.TryChangeDamage(player, new DamageSpecifier(damageSpecifier, -40));
            _damageableSystem.TryChangeDamage(player, new DamageSpecifier(damageSpecifier2, -40));
        }

        private string GetStatus(MobState mobState)
        {
            return mobState switch
            {
                MobState.Alive => Loc.GetString("health-analyzer-window-entity-alive-text"),
                MobState.Critical => Loc.GetString("health-analyzer-window-entity-critical-text"),
                MobState.Dead => Loc.GetString("health-analyzer-window-entity-dead-text"),
                _ => Loc.GetString("health-analyzer-window-entity-unknown-text"),
            };
        }

        public string GetDistance(EntityUid uid, TransformComponent xform)
        {

            if (!_entityManager.TryGetComponent<TransformComponent>(uid, out var transform))
                return string.Empty;

            if (!transform.Coordinates.TryDistance(_entityManager, xform.Coordinates, out var dist))
                return string.Empty;

            var metres = dist.ToString("0.0");

            return metres;
        }

        public bool IsCorrectLocation(EntityUid uid, out EntityCoordinates coords)
        {
            coords = default;

            var transform = CompOrNull<TransformComponent>(uid);

            if (transform == null)
                return false;

            var gridUid = transform.GridUid;

            if (!TryComp<MapGridComponent>(gridUid, out var mapGrid))
                return false;

            var position = _map.TileIndicesFor(gridUid.Value, mapGrid, transform.Coordinates);

            _intersectingEntities.Clear();
            _lookup.GetLocalEntitiesIntersecting(gridUid.Value, position, _intersectingEntities, -0.05f, LookupFlags.Uncontained);

            foreach (var ent in _intersectingEntities)
            {
                if (HasComp<CultRuneBaseComponent>(ent))
                {
                    _popupSystem.PopupCursor(Loc.GetString("tile-has-rune"), uid, PopupType.MediumCaution);
                    return false;
                }
            }
            coords = _map.GridTileToLocal(gridUid.Value, mapGrid, position);
            return true;
        }

        /*
         * Helpers End ----
         */
    }
}
