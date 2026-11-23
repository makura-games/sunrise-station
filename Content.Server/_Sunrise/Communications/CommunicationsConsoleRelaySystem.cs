using Content.Server.Communications;
using Content.Shared.Speech;
using Robust.Shared.Player;

namespace Content.Server._Sunrise.Communications;

public sealed class CommunicationsConsoleRelaySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CommunicationsConsoleComponent, ListenAttemptEvent>(OnRelayListenAttempt);
    }

    private void OnRelayListenAttempt(EntityUid uid, CommunicationsConsoleComponent comp, ListenAttemptEvent args)
    {
        if (!HasComp<ActorComponent>(args.Source))
            args.Cancel();
    }
}
