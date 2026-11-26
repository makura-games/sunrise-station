using Content.Shared.Humanoid.Markings;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Mood;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class MoodVisualsComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public ProtoId<MarkingPrototype> Marking;

    /// <summary>
    /// Показывать исходный спрайт, если данных о настроении нет.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool VisibleWithoutMood;

    /// <summary>
    /// Пороги, которых нет в словаре, скрывают маркировку.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<MoodThreshold, string> MoodStates = new();
}
