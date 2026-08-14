using Content.Shared.Humanoid.Markings;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Mood;

/// <summary>
/// Устанавливает, какие markings отображают настроение и какое состояние использовать для текущего порога.
/// </summary>
[RegisterComponent]
public sealed partial class MoodVisualsComponent : Component
{
    /// <summary>
    /// Категория, в которой система ищет выбранный marking.
    /// </summary>
    [DataField]
    public MarkingCategories MarkingCategory = MarkingCategories.Special;

    /// <summary>
    /// Markings, на которые распространяется визуализация настроения.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<MarkingPrototype>> MoodMarkings = [];

    /// <summary>
    /// Отображать marking, если данные настроения ещё не получены.
    /// </summary>
    [DataField]
    public bool VisibleWithoutMood;

    /// <summary>
    /// Словарь, сопоставляющий пороги настроения с состояниями спрайта.
    /// Если порог отсутствует в этом словаре, спрайт для этого порога отображаться не будет.
    /// </summary>
    [DataField]
    public Dictionary<MoodThreshold, string> MoodStates = new();
}
