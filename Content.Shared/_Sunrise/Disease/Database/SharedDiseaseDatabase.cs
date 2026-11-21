using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Disease.Database;

/// <summary>
/// Ключ интерфейса базы данных вирусов.
/// </summary>
[Serializable, NetSerializable]
public enum DiseaseDatabaseUiKey : byte
{
    Key,
}

/// <summary>
/// Запись базы данных по одному штамму: всё, что о нём известно станции.
/// </summary>
/// <remarks>
/// Строки передаются идентификаторами локали, а не названиями болезней:
/// настоящее имя штамма игроку не раскрывается нигде.
/// </remarks>
[Serializable, NetSerializable]
public sealed class DiseaseDatabaseEntry(
    string strainCode,
    string syndrome,
    string? classification,
    List<string> symptoms,
    int research,
    int researchGoal,
    bool solved)
{
    /// <summary>
    /// Код штамма, по которому врач связывает образцы разных пациентов.
    /// </summary>
    public readonly string StrainCode = strainCode;

    /// <summary>
    /// Синдром: как болезнь выглядит снаружи.
    /// </summary>
    public readonly string Syndrome = syndrome;

    /// <summary>
    /// Класс возбудителя, если его уже выявил анализ.
    /// </summary>
    public readonly string? Classification = classification;

    /// <summary>
    /// Симптомы, замеченные у пациентов или выявленные в культурах.
    /// </summary>
    public readonly List<string> Symptoms = symptoms;

    /// <summary>
    /// Накопленные очки исследования.
    /// </summary>
    public readonly int Research = research;

    /// <summary>
    /// Сколько очков нужно, чтобы расшифровать все свойства пробных партий.
    /// </summary>
    public readonly int ResearchGoal = researchGoal;

    /// <summary>
    /// Найден ли рецепт вакцины.
    /// </summary>
    public readonly bool Solved = solved;
}

/// <summary>
/// Запрос на выдачу пробирки с лабораторным образцом штамма.
/// </summary>
[Serializable, NetSerializable]
public sealed class DiseaseDatabaseDispenseMessage(int index) : BoundUserInterfaceMessage
{
    /// <summary>
    /// Индекс штамма в <see cref="Components.DiseaseDatabaseConsoleComponent.Entries"/>.
    /// </summary>
    public readonly int Index = index;
}
