using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Antags.Vampires.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class VampireBeamVisualComponent : Component
{
    /// <summary>
    /// Угловой сдвиг спрайта луча.
    /// </summary>
    [DataField(required: true)]
    public Angle AngleOffset;

    /// <summary>
    /// Ориентирован ли исходный спрайт вертикально.
    /// </summary>
    [DataField(required: true)]
    public bool SpriteIsVertical;

    /// <summary>
    /// Толщина луча в пикселях.
    /// </summary>
    [DataField(required: true)]
    public float Thickness;

    /// <summary>
    /// Минимальная дистанция, на которой луч отрисовывается полностью.
    /// </summary>
    [DataField(required: true)]
    public float MinDistance;

    /// <summary>
    /// Минимальная длина луча.
    /// </summary>
    [DataField(required: true)]
    public float MinLength;
}
