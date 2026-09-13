using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Robust.Shared.Random;
using Content.Shared.Speech;

namespace Content.Server.Speech.EntitySystems;

public sealed partial class LizardAccentSystem : EntitySystem
{
    private static readonly Regex RegexLowerS = new("s+");
    private static readonly Regex RegexUpperS = new("S+");
    private static readonly Regex RegexInternalX = new(@"(\w)x");
    private static readonly Regex RegexLowerEndX = new(@"\bx([\-|r|R]|\b)");
    private static readonly Regex RegexUpperEndX = new(@"\bX([\-|r|R]|\b)");
    private static readonly Regex LowerEsRegex = new("с+");
    private static readonly Regex UpperEsRegex = new("С+");
    private static readonly Regex LowerZeRegex = new("з+");
    private static readonly Regex UpperZeRegex = new("З+");
    private static readonly Regex LowerShaRegex = new("ш+");
    private static readonly Regex UpperShaRegex = new("Ш+");
    private static readonly Regex LowerCheRegex = new("ч+");
    private static readonly Regex UpperCheRegex = new("Ч+");

    [Dependency] private IRobustRandom _random = default!; // Russian-Localization

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LizardAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, LizardAccentComponent component, AccentGetEvent args)
    {
        var message = args.Message;

        // hissss
        message = RegexLowerS.Replace(message, "sss");
        // hiSSS
        message = RegexUpperS.Replace(message, "SSS");
        // ekssit
        message = RegexInternalX.Replace(message, "$1kss");
        // ecks
        message = RegexLowerEndX.Replace(message, "ecks$1");
        // eckS
        message = RegexUpperEndX.Replace(message, "ECKS$1");

        // Russian-Localization-Start
        // c => ссс
        message = LowerEsRegex.Replace(
            message,
            _random.Pick(new List<string>() { "сс", "ссс" })
        );
        // С => CCC
        message = UpperEsRegex.Replace(
            message,
            _random.Pick(new List<string>() { "СС", "ССС" })
        );
        // з => ссс
        message = LowerZeRegex.Replace(
            message,
            _random.Pick(new List<string>() { "сс", "ссс" })
        );
        // З => CCC
        message = UpperZeRegex.Replace(
            message,
            _random.Pick(new List<string>() { "СС", "ССС" })
        );
        // ш => шшш
        message = LowerShaRegex.Replace(
            message,
            _random.Pick(new List<string>() { "шш", "шшш" })
        );
        // Ш => ШШШ
        message = UpperShaRegex.Replace(
            message,
            _random.Pick(new List<string>() { "ШШ", "ШШШ" })
        );
        // ч => щщщ
        message = LowerCheRegex.Replace(
            message,
            _random.Pick(new List<string>() { "щщ", "щщщ" })
        );
        // Ч => ЩЩЩ
        message = UpperCheRegex.Replace(
            message,
            _random.Pick(new List<string>() { "ЩЩ", "ЩЩЩ" })
        );
        // Russian-Localization-End
        args.Message = message;
    }
}
