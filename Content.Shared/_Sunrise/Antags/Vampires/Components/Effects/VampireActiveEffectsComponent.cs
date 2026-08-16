using Content.Shared.FixedPoint;

namespace Content.Shared._Sunrise.Antags.Vampires.Components.Effects;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ActiveVampireRejuvenateComponent : Component
{
    public int TicksRemaining;

    public TimeSpan TickInterval = TimeSpan.FromSeconds(3.5);

    [AutoPausedField]
    public TimeSpan NextTick;

    public FixedPoint2 HealBrute;

    public FixedPoint2 HealBurn;

    public FixedPoint2 HealPoison;

    public FixedPoint2 HealAsphyxiation;
}
