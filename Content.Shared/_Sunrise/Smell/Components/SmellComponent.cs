namespace Content.Shared._Sunrise.Smell.Components;

/// <summary>
/// Marks a bearer capable of smelling entities.
/// Only entities with this component get the "smell" verb and can catch
/// the scents of other creatures.
/// </summary>
[RegisterComponent]
public sealed partial class SmellComponent : Component
{
}
