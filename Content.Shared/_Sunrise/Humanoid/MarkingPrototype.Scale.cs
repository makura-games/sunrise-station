using System.Numerics;

namespace Content.Shared.Humanoid.Markings;

public sealed partial class MarkingPrototype
{
    [DataField]
    public Vector2 Scale { get; private set; } = Vector2.One;
}
