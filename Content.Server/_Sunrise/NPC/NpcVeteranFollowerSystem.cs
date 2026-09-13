using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared._Sunrise.NPC;
using Content.Shared.Mobs.Systems;
using Content.Shared.Tag;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.NPC;

public sealed partial class NpcVeteranFollowerSystem : EntitySystem
{
    private const float FastAcquireDelay = 5f;

    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private NPCSystem _npc = default!;
    [Dependency] private TagSystem _tag = default!;

    private static readonly TimeSpan UpdateCooldown = TimeSpan.FromSeconds(1);
    private TimeSpan _nextUpdate = TimeSpan.Zero;

    private readonly HashSet<EntityUid> _nearby = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NpcVeteranFollowerComponent, MapInitEvent>(OnMapInit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + UpdateCooldown;

        var query = EntityQueryEnumerator<NpcVeteranFollowerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_tag.HasTag(uid, comp.BossTag) || !_mobState.IsAlive(uid))
                continue;

            comp.RecheckAccumulator -= (float) UpdateCooldown.TotalSeconds;
            if (comp.RecheckAccumulator > 0f)
                continue;

            comp.RecheckAccumulator = comp.RecheckCooldown;
            TryAssignFollowTarget(uid, comp);
        }
    }

    private void OnMapInit(Entity<NpcVeteranFollowerComponent> ent, ref MapInitEvent args)
    {
        if (_tag.HasTag(ent, ent.Comp.BossTag) || !_mobState.IsAlive(ent))
            return;

        var foundTarget = TryAssignFollowTarget(ent, ent.Comp);
        if (ent.Comp.RecheckCooldown <= 0f)
            return;

        // If no leader was found during MapInit, retry quickly once.
        var maxDelay = foundTarget
            ? ent.Comp.RecheckCooldown
            : MathF.Min(ent.Comp.RecheckCooldown, FastAcquireDelay);
        ent.Comp.RecheckAccumulator = _random.NextFloat() * maxDelay;
    }

    private bool TryAssignFollowTarget(EntityUid uid, NpcVeteranFollowerComponent comp)
    {
        _nearby.Clear();
        _lookup.GetEntitiesInRange(uid, comp.SearchRadius, _nearby);

        var ownXform = Transform(uid);
        var ownMap = ownXform.MapID;
        var ownPos = ownXform.MapPosition.Position;

        var isVeteran = _tag.HasTag(uid, comp.VeteranLeaderTag);

        EntityUid? closestVeteran = null;
        var closestVeteranDistance = float.MaxValue;
        EntityUid? closestBoss = null;
        var closestBossDistance = float.MaxValue;

        foreach (var candidate in _nearby)
        {
            if (candidate == uid)
                continue;

            if (!_mobState.IsAlive(candidate))
                continue;

            var candidateXform = Transform(candidate);
            if (candidateXform.MapID != ownMap)
                continue;

            var distance = (candidateXform.MapPosition.Position - ownPos).LengthSquared();
            if (_tag.HasTag(candidate, comp.BossTag))
            {
                if (distance >= closestBossDistance)
                    continue;

                closestBossDistance = distance;
                closestBoss = candidate;
                continue;
            }

            if (_tag.HasTag(candidate, comp.VeteranLeaderTag))
            {
                if (distance >= closestVeteranDistance)
                    continue;

                closestVeteranDistance = distance;
                closestVeteran = candidate;
            }
        }

        EntityUid? followTarget;
        if (isVeteran)
        {
            // Veterans group up on the nearest boss.
            followTarget = closestBoss;
        }
        else
        {
            // Regular pirates group up on veterans; fallback to boss.
            followTarget = closestVeteran ?? closestBoss;
        }

        if (followTarget == null)
        {
            // Drop stale follow target to avoid moving to a dead leader's last known position.
            ClearFollowTarget(uid);
            return false;
        }

        _npc.SetBlackboard(uid, NPCBlackboard.FollowTarget, new EntityCoordinates(followTarget.Value, Vector2.Zero));
        return true;
    }

    private void ClearFollowTarget(EntityUid uid)
    {
        if (!TryComp<HTNComponent>(uid, out var htn))
            return;

        htn.Blackboard.Remove<EntityCoordinates>(NPCBlackboard.FollowTarget);
    }
}
