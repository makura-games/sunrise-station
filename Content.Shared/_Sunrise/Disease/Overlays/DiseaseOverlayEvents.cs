using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Disease.Overlays;

/// <summary>
/// Полный список меток оверлея болезней для одного игрока. Пустой список выключает оверлей.
/// </summary>
/// <remarks>
/// Сервер шлёт его только тем, кому оверлей положен, поэтому клиент не может сам вычислить заражённых.
/// </remarks>
[Serializable, NetSerializable]
public sealed class DiseaseOverlayUpdateEvent(List<DiseaseOverlayEntry> entries) : EntityEventArgs
{
    /// <summary>
    /// Метки, которые надо нарисовать.
    /// </summary>
    public readonly List<DiseaseOverlayEntry> Entries = entries;
}

/// <summary>
/// Одна подсвеченная сущность.
/// </summary>
/// <param name="Entity">Подсвеченная сущность.</param>
/// <param name="Kind">Что это за очаг.</param>
/// <param name="Diseases">Болезни очага. Заполняется только для админского оверлея.</param>
[Serializable, NetSerializable]
public readonly record struct DiseaseOverlayEntry(
    NetEntity Entity,
    DiseaseOverlayKind Kind,
    List<DiseaseOverlayDisease> Diseases);

/// <summary>
/// Болезнь в метке админского оверлея.
/// </summary>
/// <param name="Name">Название болезни.</param>
/// <param name="StrainCode">Код штамма.</param>
/// <param name="Stage">Стадия с единицы. Ноль у предметов и облаков, у которых стадий нет.</param>
/// <param name="StageCount">Число стадий болезни.</param>
[Serializable, NetSerializable]
public readonly record struct DiseaseOverlayDisease(LocId Name, string StrainCode, int Stage, int StageCount);

/// <summary>
/// Вид очага в оверлее болезней.
/// </summary>
[Serializable, NetSerializable]
public enum DiseaseOverlayKind : byte
{
    /// <summary>
    /// Заражённый предмет.
    /// </summary>
    Source,

    /// <summary>
    /// Носитель, в котором болезнь уже развивается.
    /// </summary>
    Carrier,

    /// <summary>
    /// Заразное облако.
    /// </summary>
    Cloud,
}
