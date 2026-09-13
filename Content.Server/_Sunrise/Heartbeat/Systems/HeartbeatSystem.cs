using Content.Server._Sunrise.Heartbeat.Components;
using Content.Shared._Sunrise.Heartbeat;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Heartbeat.Systems;

// TODO: Сделать возможность с помощью стетоскопа услышать сердцебиение человека

public sealed partial class HeartbeatSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly SoundSpecifier HeartbeatSound =
        new SoundPathSpecifier("/Audio/_Sunrise/Effects/heartbeat.ogg", AudioParams.Default.WithVolume(-3f));

    private static readonly HashSet<ICommonSession> DisabledSessions = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CritHeartbeatComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<ActiveHeartbeatComponent, DamageChangedEvent>(OnDamage);

        SubscribeNetworkEvent<HeartbeatOptionsChangedEvent>(OnOptionsChanged);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => DisabledSessions.Clear());
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ActiveHeartbeatComponent>();

        while (query.MoveNext(out var uid, out var activeHeartbeat))
        {
            if (_timing.CurTime < activeHeartbeat.NextHeartbeatTime)
                continue;

            if (IsDisabledByClient(uid))
                continue;

            _audio.PlayGlobal(HeartbeatSound, uid, AudioParams.Default.WithPitchScale(activeHeartbeat.Pitch));

            SetNextTime(activeHeartbeat);
        }
    }

    /// <summary>
    /// Устанавливает время следующего удара сердца
    /// </summary>
    private void SetNextTime(ActiveHeartbeatComponent component)
    {
        component.NextHeartbeatTime = _timing.CurTime + component.NextHeartbeatCooldown;
    }

    private bool IsDisabledByClient(EntityUid player)
    {
        if (!_player.TryGetSessionByEntity(player, out var session))
            return true;

        if (DisabledSessions.Contains(session))
            return true;

        return false;
    }

    private static void OnOptionsChanged(HeartbeatOptionsChangedEvent ev, EntitySessionEventArgs args)
    {
        if (ev.Enabled)
            DisabledSessions.Remove(args.SenderSession);
        else
            DisabledSessions.Add(args.SenderSession);
    }
}
