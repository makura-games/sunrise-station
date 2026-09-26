using System.Text.RegularExpressions;

#pragma warning disable IDE0130
namespace Content.Shared.Speech.EntitySystems;

public sealed partial class FrenchAccentSystem
{
    private static readonly Regex RegexCyrillicKr = new(
        @"[кКрР]",
        RegexOptions.Compiled | RegexOptions.NonBacktracking);

    private static string AccentuateSunrise(string message)
    {
        return RegexCyrillicKr.Replace(message, static match => match.Value[0] switch
        {
            'К' => "КХ",
            'к' => "кх",
            'Р' => "Х",
            'р' => "х",
            _ => match.Value,
        });
    }
}
