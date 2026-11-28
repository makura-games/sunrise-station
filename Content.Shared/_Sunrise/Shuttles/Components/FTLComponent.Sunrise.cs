using System.Numerics;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Shuttles.Components;

public sealed partial class FTLComponent
{
    /// <summary>
    /// Позволяет игнорировать зарезервированные доки при поиске точки стыковки.
    /// </summary>
    [DataField]
    public bool Ignored;

    /// <summary>
    /// Задаёт удаление мешающих сеток после стыковки FTL.
    /// </summary>
    [DataField]
    public bool DeleteTrash;

    /// <summary>
    /// Карта, с которой шаттл стартовал в текущий FTL.
    /// Нужна, чтобы вернуть шаттл туда, если цель перелёта перестала существовать.
    /// </summary>
    [DataField]
    public EntityUid? OriginMap;

    /// <summary>
    /// Мировая позиция шаттла на момент старта FTL на <see cref="OriginMap"/>.
    /// </summary>
    [DataField]
    public Vector2 OriginPosition;

    /// <summary>
    /// FTL завершён на месте, без карты назначения.
    /// В этом состоянии не применяются эффекты прибытия (разморозка карты и Smimsh).
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public bool ArrivedInPlace;
}
