using Content.Shared.Inventory;

namespace Content.Shared._Sunrise.Flashbang;

/// <summary>
/// Поднимается направленно на сущность-цель и ретранслируется через слоты HEAD и EARS.
/// Экипировка добавляет <see cref="ProtectionDistance"/>, ослабляя итоговый эффект вспышки.
/// </summary>
public sealed class GetFlashbangProtectionEvent : EntityEventArgs, IInventoryRelayEvent
{
    /// <summary>
    /// Радиус текущей вспышки, от которого системы могут вычислить свою защиту.
    /// </summary>
    public float SourceRange;

    /// <summary>
    /// Суммарное виртуальное расстояние защиты в тайлах, накопленное из экипировки.
    /// </summary>
    public float ProtectionDistance;

    public SlotFlags TargetSlots => SlotFlags.HEAD | SlotFlags.EARS;
}

/// <summary>
/// Поднимается направленно на цель непосредственно перед применением эффекта вспышки.
/// Позволяет другим системам отменить стандартный эффект или взять обработку на себя.
/// </summary>
[ByRefEvent]
public record struct FlashbangAttemptEvent(EntityUid Source, EntityUid? User, EntityUid Target, float EffectiveDistance)
{
    /// <summary>
    /// Если true — эффект полностью отменяется (стан и падение не применяются).
    /// </summary>
    public bool Cancelled;

    /// <summary>
    /// Если true — другая система взяла обработку на себя, стандартный эффект пропускается.
    /// </summary>
    public bool Handled;
}
