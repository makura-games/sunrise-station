using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Server.Popups;
using Content.Shared.Atmos;
using Content.Shared.Chat;
using Content.Shared.Dataset;
using Content.Shared.Mobs; // Sunrise-Edit
using Content.Shared.Mobs.Components; // Sunrise-Edit
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Pointing;
using Content.Shared.Random.Helpers;
using Content.Shared.RatKing;
using Robust.Shared.Map;

namespace Content.Server.RatKing
{
    /// <inheritdoc/>
    public sealed partial class RatKingSystem : SharedRatKingSystem
    {
        [Dependency] private AtmosphereSystem _atmos = default!;
        [Dependency] private ChatSystem _chat = default!;
        [Dependency] private HTNSystem _htn = default!;
        [Dependency] private HungerSystem _hunger = default!;
        [Dependency] private NPCSystem _npc = default!;
        [Dependency] private PopupSystem _popup = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<RatKingComponent, RatKingRaiseArmyActionEvent>(OnRaiseArmy);
            SubscribeLocalEvent<RatKingComponent, RatKingRaiseGuardActionEvent>(OnRaiseGuard); // Sunrise-Edit
            SubscribeLocalEvent<RatKingComponent, RatKingDomainActionEvent>(OnDomain);
            SubscribeLocalEvent<RatKingComponent, AfterPointedAtEvent>(OnPointedAt);
        }

        /// <summary>
        /// Summons an allied rat servant at the King, costing a small amount of hunger
        /// </summary>
        private void OnRaiseArmy(EntityUid uid, RatKingComponent component, RatKingRaiseArmyActionEvent args)
        {
            if (args.Handled)
                return;

            if (!TryComp<HungerComponent>(uid, out var hunger))
                return;

            // Sunrise-Edit
            var livingServants = 0;
            foreach (var servantId in component.Servants)
            {
                if (TryComp<MobStateComponent>(servantId, out var mobState) && mobState.CurrentState != MobState.Dead)
                    livingServants++;
            }

            if (livingServants >= component.MaxArmyCount)
            // Sunrise-Edit
            {
                _popup.PopupEntity(Loc.GetString("rat-king-max-army", ("amount", component.MaxArmyCount)), uid, uid);
                return;
            }

            //make sure the hunger doesn't go into the negatives
            if (_hunger.GetHunger(hunger) < component.HungerPerArmyUse)
            {
                _popup.PopupEntity(Loc.GetString("rat-king-too-hungry"), uid, uid);
                return;
            }
            args.Handled = true;
            _hunger.ModifyHunger(uid, -component.HungerPerArmyUse, hunger);
            var servant = Spawn(component.ArmyMobSpawnId, Transform(uid).Coordinates);
            var comp = EnsureComp<RatKingServantComponent>(servant);
            comp.King = uid;
            Dirty(servant, comp);

            component.Servants.Add(servant);
            _npc.SetBlackboard(servant, NPCBlackboard.FollowTarget, new EntityCoordinates(uid, Vector2.Zero));
            UpdateServantNpc(servant, component.CurrentOrder);
        }

        // Sunrise-Start
        /// <summary>
        /// Summons an allied rat guard at the King, costing a large amount of hunger
        /// </summary>
        private void OnRaiseGuard(EntityUid uid, RatKingComponent component, RatKingRaiseGuardActionEvent args)
        {
            if (args.Handled)
                return;

            if (!TryComp<HungerComponent>(uid, out var hunger))
                return;

            // Check living guards count
            var livingGuards = 0;
            foreach (var guardId in component.Guards)
            {
                if (TryComp<MobStateComponent>(guardId, out var mobState) && mobState.CurrentState != MobState.Dead)
                    livingGuards++;
            }

            if (livingGuards >= component.MaxGuardCount)
            {
                _popup.PopupEntity(Loc.GetString("rat-king-max-guards", ("amount", component.MaxGuardCount)), uid, uid);
                return;
            }

            //make sure the hunger doesn't go into the negatives
            if (_hunger.GetHunger(hunger) < component.HungerPerGuardUse)
            {
                _popup.PopupEntity(Loc.GetString("rat-king-too-hungry"), uid, uid);
                return;
            }
            args.Handled = true;
            _hunger.ModifyHunger(uid, -component.HungerPerGuardUse, hunger);
            var guard = Spawn(component.GuardMobSpawnId, Transform(uid).Coordinates);
            var comp = EnsureComp<RatKingServantComponent>(guard);
            comp.King = uid;
            Dirty(guard, comp);

            component.Guards.Add(guard);
            _npc.SetBlackboard(guard, NPCBlackboard.FollowTarget, new EntityCoordinates(uid, Vector2.Zero));
            UpdateServantNpc(guard, component.CurrentOrder);
        }
        // Sunrise-End

        /// <summary>
        /// uses hunger to release a specific amount of ammonia into the air. This heals the rat king
        /// and his servants through a specific metabolism.
        /// </summary>
        private void OnDomain(EntityUid uid, RatKingComponent component, RatKingDomainActionEvent args)
        {
            if (args.Handled)
                return;

            if (!TryComp<HungerComponent>(uid, out var hunger))
                return;

            //make sure the hunger doesn't go into the negatives
            if (_hunger.GetHunger(hunger) < component.HungerPerDomainUse)
            {
                _popup.PopupEntity(Loc.GetString("rat-king-too-hungry"), uid, uid);
                return;
            }
            args.Handled = true;
            _hunger.ModifyHunger(uid, -component.HungerPerDomainUse, hunger);

            _popup.PopupEntity(Loc.GetString("rat-king-domain-popup"), uid);
            var tileMix = _atmos.GetTileMixture(uid, excite: true);
            tileMix?.AdjustMoles(Gas.Ammonia, component.MolesAmmoniaPerDomain);
        }

        private void OnPointedAt(EntityUid uid, RatKingComponent component, ref AfterPointedAtEvent args)
        {
            if (component.CurrentOrder != RatKingOrderType.CheeseEm)
                return;

            foreach (var servant in component.Servants)
            {
                _npc.SetBlackboard(servant, NPCBlackboard.CurrentOrderedTarget, args.Pointed);
            }

            // Sunrise-Start
            foreach (var guard in component.Guards)
            {
                _npc.SetBlackboard(guard, NPCBlackboard.CurrentOrderedTarget, args.Pointed);
            }
            // Sunrise-End
        }

        public override void UpdateServantNpc(EntityUid uid, RatKingOrderType orderType)
        {
            base.UpdateServantNpc(uid, orderType);

            if (!TryComp<HTNComponent>(uid, out var htn))
                return;

            if (htn.Plan != null)
                _htn.ShutdownPlan(htn);

            _npc.SetBlackboard(uid, NPCBlackboard.CurrentOrders, orderType);
            _htn.Replan(htn);
        }

        public override void DoCommandCallout(EntityUid uid, RatKingComponent component)
        {
            base.DoCommandCallout(uid, component);

            if (!component.OrderCallouts.TryGetValue(component.CurrentOrder, out var datasetId) ||
                !PrototypeManager.TryIndex<LocalizedDatasetPrototype>(datasetId, out var datasetPrototype))
                return;

            var msg = Random.Pick(datasetPrototype);
            _chat.TrySendInGameICMessage(uid, msg, InGameICChatType.Speak, true);
        }
    }
}
