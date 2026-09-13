using Content.Shared.Damage;

namespace Content.Shared.Blocking;

/// <summary>
/// Событие успешной попытки принять урон блокирующим предметом.
/// </summary>
public sealed class BlockingEvent : EntityEventArgs
{
    public readonly EntityUid User;
    public readonly DamageSpecifier Damage;

    public BlockingEvent(EntityUid user, DamageSpecifier damage)
    {
        User = user;
        Damage = damage;
    }
}
