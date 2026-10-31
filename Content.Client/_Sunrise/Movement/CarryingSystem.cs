using System.Linq;
using System.Numerics;
using Content.Shared._Sunrise.Movement.Carrying;
using Robust.Client.GameObjects;

namespace Content.Client._Sunrise.Movement;

public sealed partial class CarryingSystem : SharedCarryingSystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    private readonly Dictionary<EntityUid, CarriedVisualState> _visualStates = new();

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActiveCanBeCarriedComponent, ComponentStartup>(OnVisualStartup);
        SubscribeLocalEvent<ActiveCanBeCarriedComponent, AfterAutoHandleStateEvent>(OnVisualState);
        SubscribeLocalEvent<ActiveCanBeCarriedComponent, ComponentRemove>(OnVisualRemove);
    }

    /// <inheritdoc/>
    public override void Shutdown()
    {
        base.Shutdown();

        foreach (var uid in _visualStates.Keys.ToArray())
        {
            RestoreCarriedVisual(uid);
        }

        _visualStates.Clear();
    }

    private void OnVisualStartup(Entity<ActiveCanBeCarriedComponent> ent, ref ComponentStartup args)
    {
        UpdateCarriedVisual(ent);
    }

    private void OnVisualState(Entity<ActiveCanBeCarriedComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateCarriedVisual(ent);
    }

    private void OnVisualRemove(Entity<ActiveCanBeCarriedComponent> ent, ref ComponentRemove args)
    {
        RestoreCarriedVisual(ent);
    }

    /// <inheritdoc/>
    protected override void OnCarryStarted(EntityUid carrier, EntityUid target)
    {
        if (!TryComp<ActiveCanBeCarriedComponent>(target, out var activeCanBeCarried))
            return;

        UpdateCarriedVisual((target, activeCanBeCarried));
    }

    /// <inheritdoc/>
    protected override void OnCarryDropped(EntityUid carrier, EntityUid target)
    {
        RestoreCarriedVisual(target);
    }

    private void UpdateCarriedVisual(Entity<ActiveCanBeCarriedComponent> ent)
    {
        if (ent.Comp.Carrier is not { } carrier)
        {
            RestoreCarriedVisual(ent.Owner);
            return;
        }

        if (!TryComp<CarrierComponent>(carrier, out var carrierComp) ||
            !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (!_visualStates.TryGetValue(ent.Owner, out var state))
        {
            state = new CarriedVisualState(
                sprite.Offset,
                sprite.NoRotation,
                sprite.EnableDirectionOverride,
                sprite.DirectionOverride);
            _visualStates.Add(ent.Owner, state);
        }

        _sprite.SetOffset((ent.Owner, sprite), state.Offset + new Vector2(0f, carrierComp.CarriedOffset));
        sprite.NoRotation = true;
        sprite.EnableDirectionOverride = true;
        sprite.DirectionOverride = Direction.South;
    }

    private void RestoreCarriedVisual(EntityUid uid)
    {
        if (!_visualStates.Remove(uid, out var state))
            return;

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        _sprite.SetOffset((uid, sprite), state.Offset);
        sprite.NoRotation = state.NoRotation;
        sprite.EnableDirectionOverride = state.EnableDirectionOverride;
        sprite.DirectionOverride = state.DirectionOverride;
    }

    private readonly record struct CarriedVisualState(
        Vector2 Offset,
        bool NoRotation,
        bool EnableDirectionOverride,
        Direction DirectionOverride);
}
