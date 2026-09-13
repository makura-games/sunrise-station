using System.Text.RegularExpressions;
using Content.Server._Sunrise.Speech.Components;
using Content.Server.Speech;
using Content.Shared.Speech;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.Speech.EntitySystems;

public sealed partial class VulpaAccentSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;

    private static readonly Regex LowerErRegex = new("р+");
    private static readonly Regex UpperErRegex = new("Р+");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VulpaAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, VulpaAccentComponent component, AccentGetEvent args)
    {
        var message = args.Message;

        // р => ррр
        message = LowerErRegex.Replace(
            input: message,
            replacement: _random.Pick(new List<string> { "рр", "ррр" })
        );
        // Р => РРР
        message = UpperErRegex.Replace(
            input: message,
            replacement: _random.Pick(new List<string> { "РР", "РРР" })
        );

        args.Message = message;
    }
}
