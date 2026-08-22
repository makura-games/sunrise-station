using Content.Shared._Sunrise.DamageOverlay;
using Content.Shared._Sunrise.SunriseCCVars;
using Robust.Shared.Configuration;
using Robust.Shared.Network;

namespace Content.Client._Sunrise.DamageOverlay;

public sealed partial class DamageOverlaySystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private INetManager _netManager = default!;

    private bool _damageOverlayEnabled;
    private bool _damageOverlaySelf;
    private bool _damageOverlayStructures;

    public override void Initialize()
    {
        base.Initialize();

        _cfg.OnValueChanged(SunriseCCVars.DamageOverlayEnable, OnDamageOverlayEnableChanged, true);
        _cfg.OnValueChanged(SunriseCCVars.DamageOverlaySelf, OnDamageOverlaySelfChanged, true);
        _cfg.OnValueChanged(SunriseCCVars.DamageOverlayStructures, OnDamageOverlayStructuresChanged, true);

        _netManager.Connected += OnConnected;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _cfg.UnsubValueChanged(SunriseCCVars.DamageOverlayEnable, OnDamageOverlayEnableChanged);
        _cfg.UnsubValueChanged(SunriseCCVars.DamageOverlaySelf, OnDamageOverlaySelfChanged);
        _cfg.UnsubValueChanged(SunriseCCVars.DamageOverlayStructures, OnDamageOverlayStructuresChanged);
    }

    private void OnDamageOverlayEnableChanged(bool option)
    {
        _damageOverlayEnabled = option;
    }

    private void OnDamageOverlaySelfChanged(bool option)
    {
        _damageOverlaySelf = option;
    }

    private void OnDamageOverlayStructuresChanged(bool option)
    {
        _damageOverlayStructures = option;
        if (_netManager.IsConnected)
            SendDamageOverlayOptions();
    }

    private void SendDamageOverlayOptions()
    {
        RaiseNetworkEvent(new DamageOverlayOptionEvent(_damageOverlayEnabled, _damageOverlaySelf, _damageOverlayStructures));
    }

    private async void OnConnected(object? sender, NetChannelArgs e)
    {
        SendDamageOverlayOptions();
    }
}
