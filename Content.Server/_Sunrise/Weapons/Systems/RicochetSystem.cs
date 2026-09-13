// Sunrise-Edit

using System.Linq;
using System.Numerics;
using Content.Shared._Sunrise.Weapons.Components;
using Content.Shared._Sunrise.Weapons.Events;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.Weapons.Systems;

public sealed partial class RicochetSystem : EntitySystem
{
    [Dependency] private IRobustRandom _rand = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RicochetableComponent, HitScanRicochetAttemptEvent>(OnRicochetPierce);
    }

    private void OnRicochetPierce(Entity<RicochetableComponent> ent, ref HitScanRicochetAttemptEvent args)
    {
        var chance = Math.Clamp(args.Chance * ent.Comp.Chance, 0f, 1f);
        if (chance == 0) return;

        var invMatrix = _transform.GetInvWorldMatrix(ent.Owner);
        var localFrom = Vector2.Transform(args.Pos, invMatrix);

        var invNoTrans = invMatrix;
        invNoTrans.M31 = 0f;
        invNoTrans.M32 = 0f;
        var localDir = Vector2.Transform(args.Dir, invNoTrans).Normalized();

        Vector2 localNormal;
        Vector2 ptLocal;

        if (args.WorldNormal is { } worldNormal && worldNormal != Vector2.Zero)
        {
            localNormal = Vector2.Transform(worldNormal, invNoTrans).Normalized();
        }
        else
        {
            if (!TryComp<FixturesComponent>(ent, out var fixtures)
                || fixtures.Fixtures.Count == 0)
                return;

            var firstFixture = fixtures.Fixtures.FirstOrDefault().Value;
            if (firstFixture == null)
                return;

            if (firstFixture.Shape is PolygonShape poly)
            {
                if (!RayCastPolygon(poly, localFrom, localDir, out var tMin, out var edgeIndex, out ptLocal))
                {
                    // Fallback: if bullet is already deep inside the shape, raycast from inside in the reverse direction to find the boundary
                    if (!RayCastPolygon(poly, localFrom, -localDir, out tMin, out edgeIndex, out ptLocal))
                        return;
                }

                if (edgeIndex < 0 || edgeIndex >= poly.Normals.Length)
                    return;

                localNormal = poly.Normals[edgeIndex];
            }
            else if (firstFixture.Shape is PhysShapeAabb aabb)
            {
                var shape = new PolygonShape();
                shape.SetAsBox(aabb.LocalBounds);

                if (!RayCastPolygon(shape, localFrom, localDir, out var tMin, out var edgeIndex, out ptLocal))
                {
                    // Fallback
                    if (!RayCastPolygon(shape, localFrom, -localDir, out tMin, out edgeIndex, out ptLocal))
                        return;
                }

                if (edgeIndex < 0 || edgeIndex >= shape.Normals.Length)
                    return;

                localNormal = shape.Normals[edgeIndex];
            }
            else if (firstFixture.Shape is PhysShapeCircle circle)
            {
                var center = circle.Position;
                var radius = circle.Radius;
                var d = localFrom - center;
                var b = Vector2.Dot(d, localDir);
                var c = d.LengthSquared() - radius * radius;
                var disc = b * b - c;

                if (disc < 0f)
                {
                    // Fallback: reverse raycast if started inside circle
                    b = Vector2.Dot(d, -localDir);
                    disc = b * b - c;
                    if (disc < 0f) return;
                    var sqrtD = MathF.Sqrt(disc);
                    var t = -b - sqrtD >= 0f ? -b - sqrtD : -b + sqrtD;
                    if (t < 0f) return;
                    ptLocal = localFrom - localDir * t;
                }
                else
                {
                    var sqrtD = MathF.Sqrt(disc);
                    var t = -b - sqrtD >= 0f ? -b - sqrtD : -b + sqrtD;
                    if (t < 0f) return;
                    ptLocal = localFrom + localDir * t;
                }

                localNormal = (ptLocal - center).Normalized();
            }
            else
            {
                return;
            }
        }

        var dot = Vector2.Dot(localDir, localNormal);
        // If we are inside-out due to deep penetration (dot > 0), we temporarily flip normal for reflection math
        if (dot > 0f)
        {
            localNormal = -localNormal;
            dot = -dot;
        }

        var clampedDot = Math.Clamp(MathF.Abs(dot), 0f, 1f);
        var angleFactor = 2f * (1f - clampedDot);

        chance = Math.Clamp(args.Chance * ent.Comp.Chance * angleFactor, 0f, 1f);
        if (!_rand.Prob(chance)) return;

        // R = D - 2*(D·N)*N
        var reflectedLocal = localDir - (2f * dot * localNormal);

        var matrix = _transform.GetWorldMatrix(ent.Owner);
        var matrixNoTrans = matrix;
        matrixNoTrans.M31 = 0f;
        matrixNoTrans.M32 = 0f;

        var reflectedWorld = Vector2.Transform(reflectedLocal, matrixNoTrans).Normalized();

        args.Dir = reflectedWorld;
        args.Ricocheted = true;
    }

    private bool RayCastPolygon(
        PolygonShape polygon,
        Vector2 origin,
        Vector2 dir,
        out float tMin,
        out int edgeIndex,
        out Vector2 ptLocal,
        float maxT = float.MaxValue)
    {
        tMin = float.MaxValue;
        edgeIndex = -1;
        ptLocal = default;

        var verts = polygon.Vertices;
        var count = polygon.VertexCount;

        for (var i = 0; i < count; i++)
        {
            var next = (i + 1) % count;
            var v0 = verts[i];
            var v1 = verts[next];

            if (RayCastSegment(origin, dir, v0, v1, out var t) && t >= 0f && t < maxT)
            {
                if (t < tMin)
                {
                    tMin = t;
                    edgeIndex = i;
                }
            }
        }

        if (edgeIndex < 0)
            return false;

        ptLocal = origin + (dir * tMin);
        return true;
    }

    private bool RayCastSegment(Vector2 origin, Vector2 dir, Vector2 v0, Vector2 v1, out float t)
    {
        t = 0f;

        var edge = v1 - v0;
        var denom = Cross2D(edge, dir);

        if (MathF.Abs(denom) < 1e-6f)
            return false;

        var diff = origin - v0;

        var s = Cross2D(diff, dir) / denom;
        if (s is < 0f or > 1f)
            return false;

        var tRay = Cross2D(diff, edge) / denom;
        if (tRay < 0f)
            return false;

        t = tRay;
        return true;
    }

    private float Cross2D(Vector2 a, Vector2 b) => (a.X * b.Y) - (a.Y * b.X);
}
