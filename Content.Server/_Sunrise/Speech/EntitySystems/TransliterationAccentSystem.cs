using System.Text;
using Content.Server._Sunrise.Speech.Components;
using Content.Server.Speech;
using Content.Server.Speech.Components;
using Content.Server.Speech.EntitySystems;
using Content.Shared.Speech;

namespace Content.Server._Sunrise.Speech.EntitySystems;
public sealed partial class TransliterationAccentSystem : EntitySystem
{
    [Dependency] private ReplacementAccentSystem _replacement = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<TransliterationAccentComponent, AccentGetEvent>(OnAccent);
    }
    private string Accentuate(string message)
    {
        var accentedMessage = new StringBuilder(_replacement.ApplyReplacements(message, "transliteration"));
        for (var i = 0; i < accentedMessage.Length; i++)
        {
            var c = accentedMessage[i];
            accentedMessage[i] = c switch
            {
                'й' => 'q',
                'ц' => 'w',
                'у' => 'e',
                'к' => 'r',
                'е' => 't',
                'н' => 'y',
                'г' => 'u',
                'ш' => 'i',
                'щ' => 'o',
                'з' => 'p',
                'х' => '[',
                'ъ' => ']',
                'ф' => 'a',
                'ы' => 's',
                'в' => 'd',
                'а' => 'f',
                'п' => 'g',
                'р' => 'h',
                'о' => 'j',
                'л' => 'k',
                'д' => 'l',
                'ж' => ';',
                'э' => '"',
                'я' => 'z',
                'ч' => 'x',
                'с' => 'c',
                'м' => 'v',
                'и' => 'b',
                'т' => 'n',
                'ь' => 'm',
                'б' => ',',
                'ю' => '.',
                'ё' => '`',
                'Й' => 'Q',
                'Ц' => 'W',
                'У' => 'E',
                'К' => 'R',
                'Е' => 'T',
                'Н' => 'Y',
                'Г' => 'U',
                'Ш' => 'I',
                'Щ' => 'O',
                'З' => 'P',
                'Х' => '{',
                'Ъ' => '}',
                'Ф' => 'A',
                'Ы' => 'S',
                'В' => 'D',
                'А' => 'F',
                'П' => 'G',
                'Р' => 'H',
                'О' => 'J',
                'Л' => 'K',
                'Д' => 'L',
                'Ж' => ':',
                'Э' => '"',
                'Я' => 'Z',
                'Ч' => 'X',
                'С' => 'C',
                'М' => 'V',
                'И' => 'B',
                'Т' => 'N',
                'Ь' => 'M',
                'Б' => '<',
                'Ю' => '>',
                'Ё' => '~',
                'q' => 'й',
                'w' => 'ц',
                'e' => 'у',
                'r' => 'к',
                't' => 'е',
                'y' => 'н',
                'u' => 'г',
                'i' => 'ш',
                'o' => 'щ',
                'p' => 'з',
                '[' => 'х',
                ']' => 'ъ',
                'a' => 'ф',
                's' => 'ы',
                'd' => 'в',
                'f' => 'а',
                'g' => 'п',
                'h' => 'р',
                'j' => 'о',
                'k' => 'л',
                'l' => 'д',
                ';' => 'ж',
                '\'' => 'э',
                'z' => 'я',
                'x' => 'ч',
                'c' => 'с',
                'v' => 'м',
                'b' => 'и',
                'n' => 'т',
                'm' => 'ь',
                ',' => 'б',
                '`' => 'ё',
                'Q' => 'Й',
                'W' => 'Ц',
                'E' => 'У',
                'R' => 'К',
                'T' => 'Е',
                'Y' => 'Н',
                'U' => 'Г',
                'I' => 'Ш',
                'O' => 'Щ',
                'P' => 'З',
                '{' => 'Х',
                '}' => 'Ъ',
                'A' => 'Ф',
                'S' => 'Ы',
                'D' => 'В',
                'F' => 'А',
                'G' => 'П',
                'H' => 'Р',
                'J' => 'О',
                'K' => 'Л',
                'L' => 'Д',
                ':' => 'Ж',
                '"' => 'Э',
                'Z' => 'Я',
                'X' => 'Ч',
                'C' => 'С',
                'V' => 'М',
                'B' => 'И',
                'N' => 'Т',
                'M' => 'Ь',
                '<' => 'Б',
                '>' => 'Ю',
                '~' => 'Ё',
                _ => accentedMessage[i]
            };
        }
        return accentedMessage.ToString();
    }
    private void OnAccent(EntityUid uid, TransliterationAccentComponent component, AccentGetEvent args)
    {
        args.Message = Accentuate(args.Message);
    }
}
