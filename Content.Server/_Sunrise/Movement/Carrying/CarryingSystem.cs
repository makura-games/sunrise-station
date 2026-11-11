using Content.Server.Resist;
using Content.Shared._Sunrise.Movement.Carrying;
using Content.Shared.ActionBlocker;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;

namespace Content.Server._Sunrise.Movement.Carrying;

public sealed partial class CarryingSystem : SharedCarryingSystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private EscapeInventorySystem _escapeInventory = default!;

    private const float MultiplierDivisor = 2f;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActiveCanBeCarriedComponent, MoveInputEvent>(OnMoveInput);
    }

    /// <summary>
    /// Attempts to escape being carried when the carried entity provides movement input
    /// </summary>
    private void OnMoveInput(Entity<ActiveCanBeCarriedComponent> ent, ref MoveInputEvent args)
    {
        if (!TryComp<CanEscapeInventoryComponent>(ent, out var escape))
            return;

        if (args.OldMovement is (MoveButtons.None or MoveButtons.Walk))
            return;

        if (!_actionBlocker.CanInteract(ent, ent.Comp.Carrier))
            return;

        if (ent.Comp.Carrier == null)
            return;

        var multiplier = MassContest(ent.Comp.Carrier.Value, ent.Owner) / MultiplierDivisor;
        _escapeInventory.AttemptEscape(ent, ent.Comp.Carrier.Value, escape, multiplier);
    }
}
