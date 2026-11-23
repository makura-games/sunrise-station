using Robust.Shared.Maths;

namespace Content.Server._Sunrise.Shuttles.Components;

/// <summary>
/// Added to a docking port to indicate it is reserved by an incoming FTL shuttle.
/// Prevents other shuttles from targeting this dock until the reservation is cleared.
/// </summary>
[RegisterComponent]
public sealed partial class FtlReservationComponent : Component
{
    /// <summary>
    /// The shuttle that reserved this dock.
    /// </summary>
    [DataField("reservedBy")]
    public EntityUid ReservedBy;

    /// <summary>
    /// Конечная площадь шаттла на нужном гриде
    /// </summary>
    public Box2 Area;
}
