using Content.Server.Actions;
using Content.Shared._Sunrise.Abilities.Jump;
using Content.Shared._Sunrise.Abilities.Resomi;
using Content.Shared.Throwing;
using Content.Shared.Standing;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Abilities.Jump;

public sealed partial class JumpSkillSystem : EntitySystem
{
    [Dependency] private ActionsSystem _action = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<JumpSkillComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<JumpSkillComponent, JumpActionEvent>(OnJump);
    }

    private void OnStartup(EntityUid uid, JumpSkillComponent component, ComponentStartup args) => _action.AddAction(uid, component.ActionJumpId);

    private void OnJump(EntityUid uid, JumpSkillComponent component, JumpActionEvent args)
    {
        if (args.Handled || _standing.IsDown(uid))
            return;

        EnsureComp<ResomiActiveAbilityComponent>(uid);

        args.Handled = true;
        var xform = Transform(uid);
        var mapCoords = args.Target.ToMap(EntityManager, _transform);
        var direction = mapCoords.Position - xform.MapPosition.Position;

        if (direction.Length() > component.MaxThrow)
            direction = direction.Normalized() * component.MaxThrow;

        _throwing.TryThrow(uid, direction, component.ThrowSpeed, uid, component.ThrowRange);

        Timer.Spawn(TimeSpan.FromSeconds(1), () =>
        {
            if (Exists(uid))
                RemComp<ResomiActiveAbilityComponent>(uid);
        });
    }
}
