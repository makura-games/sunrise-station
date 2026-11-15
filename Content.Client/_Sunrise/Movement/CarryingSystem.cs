using System.Linq;
using System.Numerics;
using Content.Client._Sunrise.Animations;
using Content.Client._Sunrise.Animations.SpriteAnimation.Systems;
using Content.Shared._Sunrise.Movement.Carrying;
using Robust.Client.GameObjects;

namespace Content.Client._Sunrise.Movement;

public sealed partial class CarryingSystem : SharedCarryingSystem
{
    [Dependency] private SpriteAnimationSystem _spriteAnimation = default!;

    [Dependency] private SpritePoseSystem _pose = default!;

    private const string AnimationKey = "carrying";

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
            state = new CarriedVisualState(sprite.NoRotation);
            _visualStates.Add(ent.Owner, state);
        }

        _pose.Suspend((ent.Owner, sprite), Direction.South);
        _spriteAnimation.SetOffsetContribution((ent.Owner, sprite), AnimationKey, new Vector2(0f, carrierComp.CarriedOffset));
        sprite.NoRotation = true;
    }

    private void RestoreCarriedVisual(EntityUid uid)
    {
        _spriteAnimation.Stop(uid, AnimationKey);
        if (!_visualStates.Remove(uid, out var state))
            return;

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        sprite.NoRotation = state.NoRotation;
        _pose.Resume((uid, sprite));
    }

    private readonly record struct CarriedVisualState(bool NoRotation);
}
