using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Robust.Shared.Random;
using Content.Shared.Speech;

namespace Content.Server.Speech.EntitySystems;

public sealed partial class MothAccentSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!; // Russian-Localization

    private static readonly Regex RegexLowerBuzz = new Regex("z{1,3}");
    private static readonly Regex RegexUpperBuzz = new Regex("Z{1,3}");
    private static readonly Regex LowerZheRegex = new("ж+");
    private static readonly Regex UpperZheRegex = new("Ж+");
    private static readonly Regex LowerZeRegex = new("з+");
    private static readonly Regex UpperZeRegex = new("З+");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MothAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, MothAccentComponent component, AccentGetEvent args)
    {
        var message = args.Message;

        // buzzz
        message = RegexLowerBuzz.Replace(message, "zzz");
        // buZZZ
        message = RegexUpperBuzz.Replace(message, "ZZZ");

        // Russian-Localization-Start
        // ж => жжж
        message = LowerZheRegex.Replace(
            message,
            _random.Pick(new List<string>() { "жж", "жжж" })
        );
        // Ж => ЖЖЖ
        message = UpperZheRegex.Replace(
            message,
            _random.Pick(new List<string>() { "ЖЖ", "ЖЖЖ" })
        );
        // з => ссс
        message = LowerZeRegex.Replace(
            message,
            _random.Pick(new List<string>() { "зз", "ззз" })
        );
        // З => CCC
        message = UpperZeRegex.Replace(
            message,
            _random.Pick(new List<string>() { "ЗЗ", "ЗЗЗ" })
        );
        // Russian-Localization-End

        args.Message = message;
    }
}
