using Robust.Shared.Player;

// ReSharper disable once CheckNamespace
namespace Content.Server.Administration.Managers;

internal static class SunriseAdminPermissionsGuard
{
    public static bool IsBlocked(IAdminManager adminManager, ICommonSession? player)
    {
        return adminManager is AdminManager manager &&
               manager.IsSunriseAdminPermissionsUiBlocked(player);
    }
}
