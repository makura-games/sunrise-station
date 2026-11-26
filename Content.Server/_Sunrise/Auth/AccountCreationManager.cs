using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Sunrise.Auth;

/// <summary>
/// Отдаёт дату создания аккаунта, которую auth-сервер прислал при подключении игрока.
/// </summary>
public sealed partial class AccountCreationManager
{
    [Dependency] private IPlayerManager _player = default!;

    /// <summary>
    /// Минимальная правдоподобная дата: auth-сервер без поля отдаёт <see cref="DateTime.MinValue"/>.
    /// </summary>
    private static readonly DateTime MinValidCreatedTime = new(2015, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Пытается получить дату создания аккаунта подключённого игрока.
    /// </summary>
    public bool TryGetAccountCreatedTime(NetUserId userId, out DateTimeOffset createdTime)
    {
        createdTime = default;
        return _player.TryGetSessionById(userId, out var session) &&
               TryGetAccountCreatedTime(session, out createdTime);
    }

    /// <summary>
    /// Пытается получить дату создания аккаунта из данных сессии.
    /// </summary>
    public bool TryGetAccountCreatedTime(ICommonSession session, out DateTimeOffset createdTime)
    {
        createdTime = default;

        if (session.Channel.UserData.CreatedTime is not { } created)
            return false;

        // Время из auth приходит в UTC, Unspecified появляется только у локальных и гостевых данных.
        var utc = created.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(created, DateTimeKind.Utc)
            : created.ToUniversalTime();

        if (utc < MinValidCreatedTime)
            return false;

        createdTime = new DateTimeOffset(utc);
        return true;
    }

    /// <summary>
    /// Пытается получить возраст аккаунта подключённого игрока.
    /// </summary>
    public bool TryGetAccountAge(ICommonSession session, out TimeSpan age)
    {
        age = default;
        if (!TryGetAccountCreatedTime(session, out var createdTime))
            return false;

        age = DateTimeOffset.UtcNow - createdTime;
        if (age < TimeSpan.Zero)
            age = TimeSpan.Zero;

        return true;
    }
}
