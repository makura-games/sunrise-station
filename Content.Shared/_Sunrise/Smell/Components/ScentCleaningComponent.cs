namespace Content.Shared._Sunrise.Smell.Components;

/// <summary>
/// Scent-cleaner marker: an item (soap, spray, etc.) with which a player can wash
/// temporary scents off a target and temporarily mask the target's base scent.
/// </summary>
[RegisterComponent]
public sealed partial class ScentCleaningComponent : Component
{
    /// <summary>
    /// Duration of the washing DoAfter, in seconds.
    /// </summary>
    [DataField]
    public float CleanDelay = 10.0f;

    /// <summary>
    /// How long the temporary masking of the base scent lasts after washing.
    /// </summary>
    [DataField]
    public TimeSpan MaskDuration = TimeSpan.FromMinutes(5);
}
