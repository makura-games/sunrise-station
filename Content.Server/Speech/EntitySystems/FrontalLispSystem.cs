using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Robust.Shared.Random; // Russian-Localization
using Content.Shared.Speech;

namespace Content.Server.Speech.EntitySystems;

public sealed partial class FrontalLispSystem : EntitySystem
{
    // @formatter:off
    private static readonly Regex RegexUpperTh = new(@"[T]+[Ss]+|[S]+[Cc]+(?=[IiEeYy]+)|[C]+(?=[IiEeYy]+)|[P][Ss]+|([S]+[Tt]+|[T]+)(?=[Ii]+[Oo]+[Uu]*[Nn]*)|[C]+[Hh]+(?=[Ii]*[Ee]*)|[Z]+|[S]+|[X]+(?=[Ee]+)");
    private static readonly Regex RegexLowerTh = new(@"[t]+[s]+|[s]+[c]+(?=[iey]+)|[c]+(?=[iey]+)|[p][s]+|([s]+[t]+|[t]+)(?=[i]+[o]+[u]*[n]*)|[c]+[h]+(?=[i]*[e]*)|[z]+|[s]+|[x]+(?=[e]+)");
    private static readonly Regex RegexUpperEcks = new(@"[E]+[Xx]+[Cc]*|[X]+");
    private static readonly Regex RegexLowerEcks = new(@"[e]+[x]+[c]*|[x]+");
    private static readonly Regex LowerEsRegex = new("с");
    private static readonly Regex UpperEsRegex = new("С");
    private static readonly Regex LowerCheRegex = new("ч");
    private static readonly Regex UpperCheRegex = new("Ч");
    private static readonly Regex LowerTseRegex = new("ц");
    private static readonly Regex UpperTseRegex = new("Ц");
    private static readonly Regex LowerTeRegex = new(@"\B[т](?![АЕЁИОУЫЭЮЯаеёиоуыэюя])");
    private static readonly Regex UpperTeRegex = new(@"\B[Т](?![АЕЁИОУЫЭЮЯаеёиоуыэюя])");
    private static readonly Regex LowerZeRegex = new("з");
    private static readonly Regex UpperZeRegex = new("З");
    // @formatter:on

    [Dependency] private IRobustRandom _random = default!; // Russian-Localization

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FrontalLispComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, FrontalLispComponent component, AccentGetEvent args)
    {
        var message = args.Message;

        // handles ts, sc(i|e|y), c(i|e|y), ps, st(io(u|n)), ch(i|e), z, s
        message = RegexUpperTh.Replace(message, "TH");
        message = RegexLowerTh.Replace(message, "th");
        // handles ex(c), x
        message = RegexUpperEcks.Replace(message, "EKTH");
        message = RegexLowerEcks.Replace(message, "ekth");

        // Russian-Localization Start
        // с - ш
        message = LowerEsRegex.Replace(message, _random.Prob(0.90f) ? "ш" : "с");
        message = UpperEsRegex.Replace(message, _random.Prob(0.90f) ? "Ш" : "С");
        // ч - ш
        message = LowerCheRegex.Replace(message, _random.Prob(0.90f) ? "ш" : "ч");
        message = UpperCheRegex.Replace(message, _random.Prob(0.90f) ? "Ш" : "Ч");
        // ц - ч
        message = LowerTseRegex.Replace(message, _random.Prob(0.90f) ? "ч" : "ц");
        message = UpperTseRegex.Replace(message, _random.Prob(0.90f) ? "Ч" : "Ц");
        // т - ч
        message = LowerTeRegex.Replace(message, _random.Prob(0.90f) ? "ч" : "т");
        message = UpperTeRegex.Replace(message, _random.Prob(0.90f) ? "Ч" : "Т");
        // з - ж
        message = LowerZeRegex.Replace(message, _random.Prob(0.90f) ? "ж" : "з");
        message = UpperZeRegex.Replace(message, _random.Prob(0.90f) ? "Ж" : "З");
        // Russian-Localization End

        args.Message = message;
    }
}
