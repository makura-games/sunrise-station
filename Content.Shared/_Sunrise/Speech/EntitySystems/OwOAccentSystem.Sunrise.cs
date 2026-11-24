#pragma warning disable IDE0130
namespace Content.Shared.Speech.EntitySystems;

public sealed partial class OwOAccentSystem
{
    private static string AccentuateSunrise(string message)
    {
        return message
            .Replace("ты", "ти")
            .Replace("Ты", "Ти")
            .Replace("маленький", "мавенки")
            .Replace("Маленький", "Мавенки")
            .Replace("р", "в")
            .Replace("Р", "В")
            .Replace("л", "в")
            .Replace("Л", "В");
    }
}
