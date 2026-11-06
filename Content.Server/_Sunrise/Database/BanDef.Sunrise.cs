using Robust.Shared.Localization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Database;

public sealed partial class BanDef
{
    private string FormatSunriseBanMessage(ILocalizationManager localization, string expires)
    {
        return $"""
            {localization.GetString("ban-banned-1")}
            {localization.GetString("ban-banned-2", ("id", Id.ToString() ?? string.Empty))}
            {localization.GetString("ban-banned-3", ("reason", Reason))}
            {expires}
            {localization.GetString("ban-banned-4")}
            """;
    }
}
