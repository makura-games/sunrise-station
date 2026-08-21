namespace Content.Shared.NPC.Components
{
    /// <summary>
    /// Marks an entity as excluded from NPC targeting and reactions.
    /// NPC systems should treat entities with this component as non-targetable.
    /// </summary>
    [RegisterComponent]
    public sealed partial class NoTargetComponent : Component;

}
