using Content.Server._Sunrise.Speech.Components;
using Content.Server.Speech;
using Content.Shared.Speech;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.Speech.EntitySystems;

public sealed partial class SwineAccentSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SwineAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, SwineAccentComponent component, AccentGetEvent args)
    {
        var message = args.Message;

        // Prefix
        if (_random.Prob(0.20f))
        {
            var pick = _random.Next(1, 3);

            // Reverse sanitize capital
            message = message[0].ToString().ToLower() + message.Remove(0, 1);
            message = Loc.GetString($"accent-swine-prefix-{pick}") + " " + message;
        }
        args.Message = message;
    }
}
