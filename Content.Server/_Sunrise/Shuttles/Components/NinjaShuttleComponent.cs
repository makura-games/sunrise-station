using Robust.Shared.GameStates;

namespace Content.Server._Sunrise.Shuttles.Components;

[RegisterComponent]
public sealed partial class NinjaShuttleComponent : Component
{
    [ViewVariables]
    public EntityUid? AssociatedRule;
}
