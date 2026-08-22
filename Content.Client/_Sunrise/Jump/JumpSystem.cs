using Content.Shared._Sunrise.Jump;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Input;
using Robust.Shared.Configuration;
using Robust.Shared.Input.Binding;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Sunrise.Jump;

public sealed partial class JumpSystem : SharedJumpSystem
{
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private INetManager _netManager = default!;

    private TimeSpan _lastJumpTime;
    private static TimeSpan _jumpCooldown;
    private bool _jumpSoundDisabled;

    private static readonly ProtoId<EmotePrototype> EmoteJumpProto = "Jump";

    public override void Initialize()
    {
        base.Initialize();

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.Jump, InputCmdHandler.FromDelegate(Jump, handle: false, outsidePrediction: false))
            .Register<JumpSystem>();

        _cfg.OnValueChanged(SunriseCCVars.JumpSoundDisable, OnJumpSoundEnabledOptionChanged, true);
        _cfg.OnValueChanged(SunriseCCVars.JumpCooldown, OnJumpCooldownChanged, true);

        _netManager.Connected += OnConnected;
    }

    private async void OnConnected(object? sender, NetChannelArgs e)
    {
        RaiseNetworkEvent(new ClientOptionDisableJumpSoundEvent(_jumpSoundDisabled));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _cfg.UnsubValueChanged(SunriseCCVars.JumpSoundDisable, OnJumpSoundEnabledOptionChanged);
    }

    private static void OnJumpCooldownChanged(float value)
    {
        _jumpCooldown = TimeSpan.FromSeconds(value);
    }

    private void OnJumpSoundEnabledOptionChanged(bool option)
    {
        _jumpSoundDisabled = option;
        if (_netManager.IsConnected)
            RaiseNetworkEvent(new ClientOptionDisableJumpSoundEvent(_jumpSoundDisabled));
    }

    private void Jump(ICommonSession? session)
    {
        var player = session?.AttachedEntity;

        if (!Exists(player))
            return;

        var currentTime = _gameTiming.CurTime;
        if (currentTime - _lastJumpTime < _jumpCooldown)
            return;

        _lastJumpTime = currentTime;
        RaisePredictiveEvent(new PlayEmoteMessage(EmoteJumpProto));
    }
}
