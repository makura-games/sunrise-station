using Content.Server.Administration.Systems;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.Administration;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Sunrise.Greetings;

public sealed partial class GreetingsSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private BwoinkSystem _bwoinkSystem = default!;

    private bool _greetingsEnabled;
    private string _greetingsMessage = "";
    private string _greetingsAuthor = "";

    public override void Initialize()
    {
        base.Initialize();

        _cfg.OnValueChanged(SunriseCCVars.GreetingsEnable, SetGreetingsEnabled, true);
        _cfg.OnValueChanged(SunriseCCVars.GreetingsMessage, SetGreetingsMessage, true);
        _cfg.OnValueChanged(SunriseCCVars.GreetingsAuthor, SetGreetingsAuthor, true);

        SubscribeLocalEvent<PlayerFirstConnectionEvent>(OnPlayerFirstConnection);
    }

    private void SetGreetingsEnabled(bool value)
    {
        _greetingsEnabled = value;
    }

    private void SetGreetingsMessage(string value)
    {
        _greetingsMessage = value;
    }

    private void SetGreetingsAuthor(string value)
    {
        _greetingsAuthor = value;
    }

    private void OnPlayerFirstConnection(PlayerFirstConnectionEvent args)
    {
        if (!_greetingsEnabled)
            return;

        var bwoinkText = $"[color=red]{_greetingsAuthor}[/color]: {_greetingsMessage}";
        var msg = new SharedBwoinkSystem.BwoinkTextMessage(args.Session.UserId, new NetUserId(Guid.Empty), bwoinkText);
        RaiseNetworkEvent(msg, args.Session.Channel);
        var admins = _bwoinkSystem.GetTargetAdmins();

        // Notify all admins
        foreach (var channel in admins)
        {
            RaiseNetworkEvent(msg, channel);
        }
    }

    public sealed class PlayerFirstConnectionEvent : EntityEventArgs
    {
        public ICommonSession Session;

        public PlayerFirstConnectionEvent(ICommonSession session)
        {
            Session = session;
        }
    }
}
