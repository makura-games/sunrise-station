using Robust.Shared.Network;

namespace Content.Server.Administration.Systems;

internal static class AhelpSenderNameHelper
{
    public static string FormatOfflineName(string name, NetUserId userId)
    {
        return string.IsNullOrWhiteSpace(name) ? userId.ToString() : name;
    }
}
