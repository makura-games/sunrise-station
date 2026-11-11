using Content.Client._Sunrise.BloodCult;
using Content.Shared._Sunrise.Abilities.Milira;
using Content.Shared._Sunrise.Mood;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Sunrise.Mood;

public sealed partial class ClientMoodVisualizerSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private MoodVisualsSystem _moodVisuals = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    [SubscribeLocalEvent]
    public void OnAppearanceChange(Entity<MoodVisualsComponent> ent, ref AppearanceChangeEvent args)
    {
        var sprite = args.Sprite;
        if (sprite is null)
            return;

        if (!_proto.TryIndex(ent.Comp.Marking, out var prototype))
            return;

        var spriteEnt = (ent.Owner, sprite);
        var hiddenByPentagram = HasComp<PentagramComponent>(ent) && HasComp<WingToggleComponent>(ent);

        foreach (var markingSprite in prototype.Sprites)
        {
            if (markingSprite is not SpriteSpecifier.Rsi rsi)
                continue;

            var layerKey = $"{prototype.ID}-{rsi.RsiState}";
            if (!_sprite.LayerMapTryGet(spriteEnt, layerKey, out var layer, false))
                continue;

            sprite.LayerSetShader(layer, "unshaded");
            var visible = _moodVisuals.TryGetMoodState(ent, args.Component, prototype.BodyPart, rsi.RsiState, out var state);
            _sprite.LayerSetVisible(spriteEnt, layer, visible && !hiddenByPentagram);
            _sprite.LayerSetRsiState(spriteEnt, layer, state ?? rsi.RsiState);
        }
    }
}
