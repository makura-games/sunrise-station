using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Network;

namespace Content.Shared._Sunrise.Weapons.Ranged;

public sealed class BulletHoleSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    private const int MaxCount = 24;
    private const string BulletHoleState = "bullethole";
    private const float WallEdgePadding = 0.1f;

    public override void Initialize()
    {
        base.Initialize();

        if (!_net.IsServer)
            return;

        SubscribeLocalEvent<BulletHoleGeneratorComponent, ProjectileDamageDealtEvent>(OnProjectileDamageDealt);
        SubscribeLocalEvent<BulletHoleGeneratorComponent, HitscanDamageDealtEvent>(OnHitscanDamageDealt);
    }

    private void OnProjectileDamageDealt(Entity<BulletHoleGeneratorComponent> ent, ref ProjectileDamageDealtEvent args)
    {
        if (!CanCreateBulletHole(ent.Comp, args.DamageDealt))
            return;

        TryApplyBulletHole(args.Target, args.HitPosition, args.Direction, ent.Comp);
    }

    private void OnHitscanDamageDealt(Entity<BulletHoleGeneratorComponent> ent, ref HitscanDamageDealtEvent args)
    {
        if (!CanCreateBulletHole(ent.Comp, args.DamageDealt))
            return;

        if (args.HitPosition is not { } hitPosition)
            return;

        TryApplyBulletHole(args.Target, hitPosition, args.Direction, ent.Comp);
    }

    private void TryApplyBulletHole(
        EntityUid target,
        Vector2 hitPosition,
        Vector2 direction,
        BulletHoleGeneratorComponent generator)
    {
        if (_whitelist.IsWhitelistFail(generator.TargetWhitelist, target))
            return;

        if (!TryComp(target, out TransformComponent? transform) || direction.IsLengthZero())
            return;

        var appearance = EnsureComp<AppearanceComponent>(target);
        var bulletHole = EnsureComp<BulletHoleComponent>(target);

        if (bulletHole.Holes.Count >= MaxCount)
            return;

        var hitCoordinates = _transform.ToCoordinates((target, transform), new MapCoordinates(hitPosition, transform.MapID));
        var localShotDirection = (direction.ToWorldAngle() - _transform.GetWorldRotation(transform)).ToWorldVec();
        var offset = GetShooterFacingOffset(hitCoordinates.Position, localShotDirection);
        var rotation = localShotDirection.ToWorldAngle();
        bulletHole.Holes.Add(new BulletHoleVisualData(BulletHoleState, offset, rotation));
        _appearance.SetData(target, BulletHoleVisuals.Holes, new BulletHoleVisualsData(bulletHole.Holes), appearance);
    }

    private static bool CanCreateBulletHole(BulletHoleGeneratorComponent generator, DamageSpecifier damage)
    {
        return damage.DamageDict.TryGetValue(generator.RequiredDamageType, out var value) && value > 0;
    }

    private static Vector2 GetShooterFacingOffset(Vector2 hitPosition, Vector2 localShotDirection)
    {
        var wallLimit = 0.5f - WallEdgePadding;
        var shooterDirection = -localShotDirection;

        if (MathF.Abs(shooterDirection.X) >= MathF.Abs(shooterDirection.Y))
        {
            return new Vector2(
                MathF.CopySign(wallLimit, shooterDirection.X),
                Math.Clamp(hitPosition.Y, -wallLimit, wallLimit));
        }

        return new Vector2(
            Math.Clamp(hitPosition.X, -wallLimit, wallLimit),
            MathF.CopySign(wallLimit, shooterDirection.Y));
    }
}
