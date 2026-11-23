using Robust.Shared.Network;

// ReSharper disable once CheckNamespace
namespace Content.Server.Database;

public sealed partial class UserDbDataManager
{
    private void AddSunriseUserData(NetUserId userId, UserData data)
    {
        if (!_users.TryAdd(userId, data))
            _sawmill.Warning($"User data cache already contains {userId} during client connect.");
    }
}
