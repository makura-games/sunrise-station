using Content.Shared._Sunrise.Heartbeat;
using Content.Shared._Sunrise.SunriseCCVars;
using Robust.Shared.Configuration;
using Robust.Shared.Network;

namespace Content.Client._Sunrise.Heartbeat;

public sealed partial class HeartbeatSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private INetManager _netManager = default!;

    private bool _playHeartBeatSound;

    public override void Initialize()
    {
        base.Initialize();

        _cfg.OnValueChanged(SunriseCCVars.PlayHeartBeatSound, OnOptionsChanged, true);

        _netManager.Connected += OnConnected;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _cfg.UnsubValueChanged(SunriseCCVars.PlayHeartBeatSound, OnOptionsChanged);
    }

    private void OnOptionsChanged(bool option)
    {
        _playHeartBeatSound = option;
        if (_netManager.IsConnected)
            RaiseNetworkEvent(new HeartbeatOptionsChangedEvent(_playHeartBeatSound));
    }

    private async void OnConnected(object? sender, NetChannelArgs e)
    {
        RaiseNetworkEvent(new HeartbeatOptionsChangedEvent(_playHeartBeatSound));
    }
}
