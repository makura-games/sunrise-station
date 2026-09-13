using Content.Server.Speech;
using Content.Shared.Speech;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.Misc.ShiftedAsciiTableAccent;

public sealed partial class AnomalyAccentSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;

    // Значения сдвига по юникоду
    private const int ShiftMin = 5;
    private const int ShiftMax = 200;

    public override void Initialize()
    {
        SubscribeLocalEvent<AnomalyAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(Entity<AnomalyAccentComponent> ent, ref AccentGetEvent args)
    {
        args.Message = Accentuate(args.Message);
    }

    private string Accentuate(string message)
    {
        var speechArray = message.ToCharArray();

        for (var i = 0; i < speechArray.Length; i++)
        {
            if (!_random.Prob(0.5f))
                continue;

            // Сдвиг символа по алфавиту юникода
            speechArray[i] = (char)(speechArray[i] + _random.Next(ShiftMin, ShiftMax));
        }

        return new string(speechArray);
    }
}
