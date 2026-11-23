using System.Text.RegularExpressions;
using Content.Shared.Random.Helpers;
using Content.Shared.Speech.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

#pragma warning disable IDE0130
namespace Content.Shared.Speech.EntitySystems;

public sealed partial class MothAccentSystem
{
    [Dependency] private IGameTiming _sunriseTiming = default!;
    [Dependency] private IRobustRandom _sunriseRandom = default!;

    private static readonly Regex LowerZheRegex = new("ж+");
    private static readonly Regex UpperZheRegex = new("Ж+");
    private static readonly Regex LowerZeRegex = new("з+");
    private static readonly Regex UpperZeRegex = new("З+");

    private string AccentuateSunrise(string message, Entity<MothAccentComponent>? ent)
    {
        var random = ent.HasValue
            ? SharedRandomExtensions.PredictedRandom(_sunriseTiming, GetNetEntity(ent.Value))
            : _sunriseRandom;

        string Pick(string first, string second) => random.Prob(0.5f) ? first : second;

        message = LowerZheRegex.Replace(message, Pick("жж", "жжж"));
        message = UpperZheRegex.Replace(message, Pick("ЖЖ", "ЖЖЖ"));
        message = LowerZeRegex.Replace(message, Pick("зз", "ззз"));
        return UpperZeRegex.Replace(message, Pick("ЗЗ", "ЗЗЗ"));
    }
}
