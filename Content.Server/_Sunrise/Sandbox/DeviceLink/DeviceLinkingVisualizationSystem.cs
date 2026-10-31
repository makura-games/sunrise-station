using Content.Shared._Sunrise.Sandbox;
using Content.Shared.DeviceLinking;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Sandbox.DeviceLink;

public sealed partial class DeviceLinkingVisualizationSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;

    private TimeSpan _nextOverlayUpdate = TimeSpan.Zero;
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    private readonly HashSet<ICommonSession> _debugSessions = [];

    public override void Initialize()
    {
        base.Initialize();

        _player.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _debugSessions.Clear();

        _player.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_debugSessions.Count == 0 || _timing.CurTime < _nextOverlayUpdate)
            return;

        _nextOverlayUpdate = _timing.CurTime + UpdateInterval;

        UpdateOverlay();
    }

    /// <summary>
    ///     Toggles the display of connected networks.
    /// </summary>
    public void ToggleDebugView(ICommonSession session)
    {
        bool isEnabled;
        if (_debugSessions.Add(session))
        {
            isEnabled = true;
        }
        else
        {
            _debugSessions.Remove(session);
            isEnabled = false;
        }

        var ev = new DeviceLinkOverlayToggledEvent(isEnabled);
        RaiseNetworkEvent(ev, session.Channel);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus != SessionStatus.Disconnected || e.OldStatus != SessionStatus.InGame)
            return;

        if (!_debugSessions.Contains(e.Session))
            return;

        _debugSessions.Remove(e.Session);
    }

    private void UpdateOverlay()
    {
        if (_debugSessions.Count == 0)
            return;

        List<DebugEntityConnectionData> rays = [];

        var query = AllEntityQuery<DeviceLinkSourceComponent>();
        while (query.MoveNext(out var uid, out var source))
        {
            if (source.LinkedPorts.Count == 0)
                continue;

            var netUid = GetNetEntity(uid);
            List<NetEntity> entities = [];

            foreach (var output in source.LinkedPorts)
            {
                entities.Add(GetNetEntity(output.Key));
            }

            rays.Add(new DebugEntityConnectionData(netUid, entities));
        }

        foreach (var session in _debugSessions)
        {
            RaiseNetworkEvent(new DeviceLinkOverlayDataEvent(rays), session);
        }
    }
}
