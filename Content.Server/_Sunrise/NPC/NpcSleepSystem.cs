using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Server.Salvage.Expeditions;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.CCVar;
using Content.Shared.Ghost;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.NPC;

public sealed partial class NpcSleepSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private NPCSystem _npc = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    public bool Enabled = true;
    public bool DisableWithoutPlayers = true;

    public float DisableDistance = 20f;

    private TimeSpan _nextCheckTime = TimeSpan.Zero;
    private static readonly TimeSpan CheckCooldown = TimeSpan.FromSeconds(5);

    [Dependency] private EntityQuery<ActorComponent> _actorQuery = default!;
    [Dependency] private EntityQuery<ActiveNPCComponent> _activeQuery = default!;
    [Dependency] private EntityQuery<GhostComponent> _ghostQuery = default!;

    private readonly HashSet<Entity<ActorComponent>> _players = [];

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_configuration, CCVars.NPCEnabled, value => Enabled = value, true);
        Subs.CVar(_configuration, SunriseCCVars.NpcDisableWithoutPlayers, obj => DisableWithoutPlayers = obj, true);
        Subs.CVar(_configuration, SunriseCCVars.NpcDisableDistance, obj => DisableDistance = obj, true);

    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!Enabled || !DisableWithoutPlayers)
            return;

        if (_timing.CurTime < _nextCheckTime)
            return;

        _nextCheckTime = _timing.CurTime + CheckCooldown;

        var query = EntityQueryEnumerator<HTNComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var htn, out var xform))
        {
            if (!_mobState.IsAlive(uid))
                continue;

            if (_actorQuery.HasComp(uid))
                continue;

            var isActive = _activeQuery.HasComponent(uid);
            if (AllowNpc(uid, xform))
            {
                if (!isActive)
                    _npc.WakeNPC(uid, htn);
            }
            else
            {
                if (isActive)
                    _npc.SleepNPC(uid, htn);
            }
        }
    }

    private bool AllowNpc(EntityUid uid, TransformComponent xform)
    {
        // НЕ СПАТЬ НА ЭКСПЕ!!
        if (xform.MapUid is { } mapUid && HasComp<SalvageExpeditionComponent>(mapUid))
            return true;

        var npcCoords = _transform.GetMapCoordinates(uid, xform);

        _players.Clear();
        _lookup.GetEntitiesInRange(npcCoords,
            DisableDistance,
            _players,
            LookupFlags.Dynamic | LookupFlags.Approximate);

        foreach (var ent in _players)
        {
            if (_ghostQuery.HasComp(ent))
                continue;

            // Достаточно одного подходящего игрока
            return true;
        }

        return false;
    }
}
