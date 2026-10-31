using System.Linq;
using Content.Server._Sunrise.Helpers;
using Content.Shared.Humanoid;
using Content.Shared.Whitelist;
using Content.Shared.Xenoarchaeology.Artifact;
using Content.Shared.Xenoarchaeology.Artifact.XAE;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.Research.Artifact.Effects.WhitelistSwap;

public sealed partial class ArtifactWhitelistSwapSystem : BaseXAESystem<ArtifactWhitelistSwapComponent>
{
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SunriseHelpersSystem _helpers = default!;
    [Dependency] private IRobustRandom _random = default!;

    protected override void OnActivated(Entity<ArtifactWhitelistSwapComponent> ent, ref XenoArtifactNodeActivatedEvent args)
    {
        var humans = _helpers.GetAll<HumanoidProfileComponent, TransformComponent>().ToList();
        if (humans.Count == 0)
            return;

        var player = _random.PickAndTake(humans);

        var targets = _helpers.GetAll<TransformComponent>()
            .Where(e => IsAllowedTarget(e, player, ent))
            .ToList();
        if (targets.Count == 0)
            return;

        var target = _random.PickAndTake(targets);

        _transform.SwapPositions((player, player.Comp2), target.AsNullable());
    }

    private bool IsAllowedTarget(Entity<TransformComponent> target,
        Entity<HumanoidProfileComponent, TransformComponent> player,
        Entity<ArtifactWhitelistSwapComponent> artifact)
    {
        if (!IsAllowedMap(target, player, artifact))
            return false;

        if (!_whitelist.CheckBoth(target, artifact.Comp.TargetBlacklist, artifact.Comp.TargetWhitelist))
            return false;

        return true;
    }

    private bool IsAllowedMap(Entity<TransformComponent> target,
        Entity<HumanoidProfileComponent, TransformComponent> player,
        Entity<ArtifactWhitelistSwapComponent> artifact)
    {
        if (target.Comp.MapID == MapId.Nullspace)
            return false;

        if (artifact.Comp.PreventTeleportFromOtherMaps && player.Comp2.MapID != target.Comp.MapID)
            return false;

        if (!target.Comp.MapUid.HasValue)
            return false;

        if (!_whitelist.CheckBoth(target.Comp.MapUid, artifact.Comp.OtherMapBlacklist, artifact.Comp.OtherMapWhitelist))
            return false;

        return true;
    }
}
