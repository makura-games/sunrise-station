using Content.Shared.Charges.Components;

namespace Content.Shared.Charges.Systems;

public abstract partial class SharedChargesSystem
{
    /// <summary>
    /// Изменяет длительность восстановления, сохраняя указанный прогресс до следующего заряда.
    /// </summary>
    public void SetRechargeDuration(
        Entity<LimitedChargesComponent?, AutoRechargeComponent?> action,
        TimeSpan duration,
        float rechargeProgress = 0f)
    {
        if (!Resolve(action.Owner, ref action.Comp1, ref action.Comp2))
            return;

        var adjustedDuration = duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
        var adjustedProgress = Math.Clamp(rechargeProgress, 0f, 1f);

        action.Comp2.RechargeDuration = adjustedDuration;
        action.Comp1.LastUpdate = _timing.CurTime -
                                  TimeSpan.FromTicks((long)(adjustedDuration.Ticks * adjustedProgress));

        // Грязный метод, грязная реализация, но это работает
        Dirty(action.Owner, action.Comp1);
        Dirty(action.Owner, action.Comp2);
    }
}
