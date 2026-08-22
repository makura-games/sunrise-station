using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Content.Shared.Speech;
using Robust.Shared.Random;

namespace Content.Server.Speech.EntitySystems
{
    public sealed partial class TajaranAccentSystem : EntitySystem
    {
        [Dependency] private IRobustRandom _random = default!;

        private static readonly Regex LowerLatinErRegex = new("r+");
        private static readonly Regex UpperLatinErRegex = new("R+");
        private static readonly Regex LowerCyrillicErRegex = new("р+");
        private static readonly Regex UpperCyrillicErRegex = new("Р+");

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<TajaranAccentComponent, AccentGetEvent>(OnAccent);
        }

        private void OnAccent(EntityUid uid, TajaranAccentComponent component, AccentGetEvent args)
        {
            var message = args.Message;

            // r => rrr
            message = LowerLatinErRegex.Replace(
                message,
                _random.Pick(new List<string> { "rr", "rrr" })
            );
            // R => RRR
            message = UpperLatinErRegex.Replace(
                message,
                _random.Pick(new List<string> { "RR", "RRR" })
            );

            // р => ррр
            message = LowerCyrillicErRegex.Replace(
                message,
                _random.Pick(new List<string> { "рр", "ррр" })
            );
            // Р => РРР
            message = UpperCyrillicErRegex.Replace(
                message,
                _random.Pick(new List<string> { "РР", "РРР" })
            );

            // Вставка апострофов
            message = AddApostrophes(message);

            args.Message = message;
        }

        private string AddApostrophes(string message)
        {
            var words = message.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 6 && _random.NextDouble() < 0.3)
                {
                    var index = _random.Next(1, words[i].Length - 1);
                    words[i] = words[i].Insert(index, "'");
                }
            }
            return string.Join(' ', words);
        }
    }
}
