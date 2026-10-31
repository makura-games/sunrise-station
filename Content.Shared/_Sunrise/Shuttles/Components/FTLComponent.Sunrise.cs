#pragma warning disable IDE0130 // Пространство имён vanilla-компонента сохраняется для partial-расширения.
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
}
