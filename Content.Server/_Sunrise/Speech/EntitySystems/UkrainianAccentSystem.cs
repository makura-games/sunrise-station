using System.Text;
using System.Text.RegularExpressions;
using Content.Server._Sunrise.Speech.Components;
using Content.Server._Sunrise.TTS;
using Content.Server.Speech.EntitySystems;
using Content.Shared.Speech;

namespace Content.Server._Sunrise.Speech.EntitySystems;

public sealed partial class UkrainianAccentSystem : EntitySystem
{
    [Dependency] private ReplacementAccentSystem _replacement = default!;

    private static readonly Regex UkrainianIRegex = new("[іІ]");
    private static readonly Regex UkrainianYiRegex = new("[їЇ]");
    private static readonly Regex UkrainianYeRegex = new("[єЄ]");
    private static readonly Regex UkrainianGeRegex = new("[ґҐ]");
    private static readonly Regex RussianYeRegex = new("[еЕ]");

    public override void Initialize()
    {
        SubscribeLocalEvent<UkrainianAccentComponent, AccentGetEvent>(OnAccent);
        SubscribeLocalEvent<UkrainianAccentComponent, TTSSanitizeEvent>(OnSanitize);
    }

    private string Accentuate(string message)
    {
        var accentedMessage = new StringBuilder(_replacement.ApplyReplacements(message, "ukrainian"));

        for (var i = 0; i < accentedMessage.Length; i++)
        {
            var c = accentedMessage[i];

            accentedMessage[i] = c switch
            {
                'и' => 'і',
                'И' => 'І',
                'ы' => 'и',
                'Ы' => 'И',
                'ё' => 'ї',
                'Ё' => 'Ї',
                'е' => 'є',
                'Е' => 'Є',
                _ => accentedMessage[i]
            };
        }

        return accentedMessage.ToString();
    }

    private void OnAccent(EntityUid uid, UkrainianAccentComponent component, AccentGetEvent args)
    {
        args.Message = Accentuate(args.Message);
    }

    private void OnSanitize(EntityUid uid, UkrainianAccentComponent component, TTSSanitizeEvent args)
    {
        var text = args.Text.Trim();
        text = UkrainianIRegex.Replace(text, "[иИ]");
        text = UkrainianYiRegex.Replace(text, "[ёЁ]");
        text = UkrainianYeRegex.Replace(text, "[еЕ]");
        text = UkrainianGeRegex.Replace(text, "[гГ]");
        text = RussianYeRegex.Replace(text, "[эЭ]");
        text = text.Trim();
        args.Text = text;
    }
}
