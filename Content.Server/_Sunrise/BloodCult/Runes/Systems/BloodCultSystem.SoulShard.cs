using Content.Server._Sunrise.BloodCult.Runes.Comps;
using Content.Server.Roles;
using Content.Shared._Sunrise.BloodCult.Components;
using Content.Shared._Sunrise.BloodCult.Items;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Roles.Components;

namespace Content.Server._Sunrise.BloodCult.Runes.Systems
{
    public partial class BloodCultSystem
    {
        public void InitializeSoulShard()
        {
            SubscribeLocalEvent<SoulShardComponent, AfterInteractEvent>(OnShardInteractUse);
            SubscribeLocalEvent<SoulShardComponent, MindAddedMessage>(OnShardMindAdded);
            SubscribeLocalEvent<SoulShardComponent, MindRemovedMessage>(OnShardMindRemoved);
        }

        private void OnShardInteractUse(EntityUid uid, SoulShardComponent component, AfterInteractEvent args)
        {
            if (!HasComp<BloodCultistComponent>(args.User))
                return;

            if (args.Target is not { } target)
                return;

            if (!TryComp<MobStateComponent>(target, out var state) || state.CurrentState != MobState.Dead)
                return;

            if (!TryComp<MindContainerComponent>(target, out var mindComponent) || mindComponent.Mind is not { } mind ||
                !TryComp<HumanoidProfileComponent>(target, out _))
                return;

            _mindSystem.TransferTo(mind, uid);

            var targetName = MetaData(target).EntityName;

            _metaDataSystem.SetEntityName(uid,
                Robust.Shared.Localization.Loc.GetString("soul-shard-description", ("soul", targetName)));
            _metaDataSystem.SetEntityDescription(uid,
                Robust.Shared.Localization.Loc.GetString("soul-shard-description", ("soul", targetName)));
        }

        private void OnShardMindAdded(EntityUid uid, SoulShardComponent component, MindAddedMessage args)
        {
            if (_roleSystem.MindHasRole<TraitorRoleComponent>(args.Mind))
                _roleSystem.MindRemoveRole<TraitorRoleComponent>(args.Mind.AsNullable());

            _appearanceSystem.SetData(uid, SoulShardVisualState.State, true);
            _lightSystem.SetEnabled(uid, true);
        }

        private void OnShardMindRemoved(EntityUid uid, SoulShardComponent component, MindRemovedMessage args)
        {
            _appearanceSystem.SetData(uid, SoulShardVisualState.State, false);
            _lightSystem.SetEnabled(uid, false);
        }
    }
}
