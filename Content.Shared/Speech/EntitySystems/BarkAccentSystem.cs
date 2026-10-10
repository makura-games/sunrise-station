using System.Collections.Frozen;
using Content.Shared.Random.Helpers;
using Content.Shared.Speech.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.Speech.EntitySystems;

public sealed partial class BarkAccentSystem : RelayAccentSystem<BarkAccentComponent>
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;

    // Sunrise edit start - русская локализация лая
    private static readonly IReadOnlyList<string> Barks =
    [
        " Гав!", " ГАВ", " вуф-вуф",
    ];
    // Sunrise edit end

    private static readonly FrozenDictionary<string, string> SpecialWords =
        new Dictionary<string, string>
        {
            { "ah", "arf" },
            { "Ah", "Arf" },
            { "oh", "oof" },
            { "Oh", "Oof" },
            // Sunrise edit start - русская локализация лая
            { "ага", "гаф" },
            { "Ага", "Гаф" },
            { "угу", "вуф" },
            { "Угу", "Вуф" },
            // Sunrise edit end
        }.ToFrozenDictionary();

    public override string Accentuate(string message, Entity<BarkAccentComponent>? ent = null)
    {
        var random = ent.HasValue
            ? SharedRandomExtensions.PredictedRandom(_timing, GetNetEntity(ent.Value))
            : _random;

        foreach (var (word, repl) in SpecialWords)
        {
            message = message.Replace(word, repl);
        }

        return message.Replace("!", random.Pick(Barks))
            .Replace("l", "r")
            .Replace("L", "R")
            // Sunrise edit start - русская локализация лая
            .Replace("л", "р")
            .Replace("Л", "Р");
            // Sunrise edit end
    }
}
