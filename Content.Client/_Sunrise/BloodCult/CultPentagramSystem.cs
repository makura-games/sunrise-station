using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Client._Sunrise.BloodCult;

public sealed partial class CultPentagramSystem : EntitySystem
{
    private const string Rsi = "_Sunrise/BloodCult/pentagram.rsi";

    private static readonly string[] States =
    {
        "halo1",
        "halo2",
        "halo3",
        "halo4",
        "halo5",
        "halo6"
    };

    [Dependency] private IRobustRandom _robustRandom = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PentagramComponent, ComponentStartup>(PentagramAdded);
        SubscribeLocalEvent<PentagramComponent, ComponentShutdown>(PentagramRemoved);
    }

    private void PentagramAdded(EntityUid uid, PentagramComponent component, ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (sprite.LayerMapTryGet(PentagramKey.Key, out var _))
            return;

        var adj = sprite.Bounds.Height / 2 + ((1.0f / 32) * 10.0f);

        var randomIndex = _robustRandom.Next(0, States.Length);

        var randomState = States[randomIndex];

        var layer = sprite.AddLayer(new SpriteSpecifier.Rsi(new ResPath(Rsi), randomState));

        sprite.LayerMapSet(PentagramKey.Key, layer);
        sprite.LayerSetOffset(layer, new Vector2(0.0f, adj));
    }

    private void PentagramRemoved(EntityUid uid, PentagramComponent component, ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (!sprite.LayerMapTryGet(PentagramKey.Key, out var layer))
            return;

        sprite.RemoveLayer(layer);
    }

    private enum PentagramKey
    {
        Key
    }
}
