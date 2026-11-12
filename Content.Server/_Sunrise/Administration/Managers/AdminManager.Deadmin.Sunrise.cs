using Content.Shared.Administration;
using Robust.Shared.Network;

namespace Content.Server.Administration.Managers;

// Sunrise-start - деадмин сохраняется при переподключении
public sealed partial class AdminManager
{
    /// <summary>
    /// Админы, которые сделали deadmin и ещё не сделали readmin.
    /// При входе права загружаются заново (из БД, Stellar RBAC или как у хоста), и без этого списка
    /// админка снова включалась после переподключения. Список живёт до перезапуска сервера.
    /// </summary>
    private readonly HashSet<NetUserId> _deadminnedUsers = new();

    /// <summary>
    /// Запоминает, что админ сделал deadmin, или забывает это после readmin.
    /// </summary>
    private void SetDeadminned(NetUserId userId, bool deadminned)
    {
        if (deadminned)
            _deadminnedUsers.Add(userId);
        else
            _deadminnedUsers.Remove(userId);
    }

    /// <summary>
    /// Выключает загруженные права, если админ сделал deadmin до переподключения или перезагрузки прав.
    /// </summary>
    private void ApplyDeadminned(NetUserId userId, AdminData data)
    {
        if (_deadminnedUsers.Contains(userId))
            data.Active = false;
    }
}
// Sunrise-end
