using System.Numerics;
using Content.Server._Sunrise.Boss.Components;
using Content.Server.Stunnable;
using Content.Shared._Sunrise.Boss.Components;
using Content.Shared._Sunrise.Boss.Events;
using Content.Shared.Actions;
using Robust.Shared.Map;

namespace Content.Server._Sunrise.Boss.Systems;

public sealed partial class HellSpawnSpiralSystem : EntitySystem
{
    [Dependency] private StunSystem _stun = default!;
    [Dependency] private SharedActionsSystem _actions = default!;

    [DataField]
    public string BulletProto = "BulletSkyFlare";

    [DataField]
    public TimeSpan TimeOffset = TimeSpan.FromSeconds(0.5);

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<HellSpawnSpiralComponent, HellSpawnSpiralActionEvent>(OnSpiral);
        SubscribeLocalEvent<HellSpawnSpiralComponent, ComponentInit>(OnInit);
    }

    private void OnInit(EntityUid uid, HellSpawnSpiralComponent component, ComponentInit args)
    {
        _actions.AddAction(uid, ref component.SpiralActionEntity, component.SpiralAction);
    }

    private async void OnSpiral(EntityUid uid, HellSpawnSpiralComponent component, HellSpawnSpiralActionEvent args)
    {
        var xform = Transform(uid);
        _stun.TryAddStunDuration(uid, TimeSpan.FromSeconds(2));

        var vectors = GenerateVectors(component.FireballCount);
        foreach (var vector in vectors)
        {
            SpawnSpiral(xform.Coordinates, vector);
        }

        args.Handled = true;
    }

    private List<Vector2> GenerateVectors(int N)
    {
        var vectors = new List<Vector2>();
        var angleStep = 2 * Math.PI / N;

        for (var i = 0; i < N; i++)
        {
            var angle = i * angleStep;
            var x = (float)Math.Cos(angle);
            var y = (float)Math.Sin(angle);
            vectors.Add(new Vector2(x, y));
        }
        return vectors;
    }

    public void SpawnSpiral(EntityCoordinates origin, Vector2 offset)
    {
        var bullet = SpawnAtPosition(BulletProto,
            new EntityCoordinates(origin.EntityId, origin.Position));
        var spiralComp = EnsureComp<SpiralMovementComponent>(bullet);
        spiralComp.TimeOffset = TimeOffset;
        spiralComp.Offset = (float)Math.Acos(offset.X);

        if (offset.Y < 0)
        {
            spiralComp.Offset = -spiralComp.Offset;
        }
    }
}
