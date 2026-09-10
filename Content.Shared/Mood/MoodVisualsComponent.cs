using Content.Shared.Humanoid.Markings;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Mood;

/// <summary>
/// Настраивает markings, состояние RSI которых зависит от настроения сущности.
/// </summary>
[RegisterComponent]
public sealed partial class MoodVisualsComponent : Component
{
    /// <summary>
    /// Marking, на который влияет визуализация настроения.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<MarkingPrototype> Marking;

    /// <summary>
    /// Должны ли настроенные markings отображаться до появления данных о настроении.
    /// </summary>
    [DataField]
    public bool VisibleWithoutMood;

    /// <summary>
    /// Сопоставляет пороги настроения с состояниями RSI.
    /// Если порог отсутствует, настроенные markings скрываются.
    /// </summary>
    [DataField]
    public Dictionary<MoodThreshold, string> MoodStates = new();
}
