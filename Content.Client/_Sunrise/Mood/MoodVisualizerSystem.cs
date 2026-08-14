using Content.Client._Sunrise.BloodCult;
using Content.Client._Sunrise.Humanoid;
using Content.Shared._Sunrise.Abilities.Milira;
using Content.Shared._Sunrise.Mood;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._Sunrise.Mood;

/// <summary>
/// Обрабатывает отображение эффектов настроения на сущностях с компонентом настроения.
/// </summary>
public sealed class MoodVisualizerSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly MarkingManager _marking = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MoodVisualsComponent, AppearanceChangeEvent>(OnAppearanceChange);
        SubscribeLocalEvent<MoodVisualsComponent, HumanoidAppearanceUpdatedEvent>(OnHumanoidAppearanceUpdated);
    }

    private void OnHumanoidAppearanceUpdated(Entity<MoodVisualsComponent> ent, ref HumanoidAppearanceUpdatedEvent args)
        => UpdateAppearance(ent);

    private void OnAppearanceChange(Entity<MoodVisualsComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite is not { } sprite)
            return;

        UpdateAppearance((ent, ent.Comp, sprite), args.Component);
    }

    private void UpdateAppearance(Entity<MoodVisualsComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite) ||
            !TryComp<AppearanceComponent>(ent, out var appearance))
        {
            return;
        }

        UpdateAppearance((ent, ent.Comp, sprite), appearance);
    }

    private void UpdateAppearance(Entity<MoodVisualsComponent, SpriteComponent> ent, AppearanceComponent appearance)
    {
        var moodVisuals = ent.Comp1;
        var sprite = ent.Comp2;

        if (!TryGetMoodLayer(ent, out var layer))
            return;

        sprite.LayerSetShader(layer, "unshaded");

        if (HasComp<PentagramComponent>(ent) && HasComp<WingToggleComponent>(ent))
        {
            _sprite.LayerSetVisible((ent, sprite), layer, false);
            return;
        }

        if (!_appearance.TryGetData<MoodThreshold>(ent, MoodVisuals.CurrentMoodThreshold, out var moodThreshold, appearance))
        {
            _sprite.LayerSetVisible((ent, sprite), layer, moodVisuals.VisibleWithoutMood);
            return;
        }

        if (!moodVisuals.MoodStates.TryGetValue(moodThreshold, out var state))
        {
            _sprite.LayerSetVisible((ent, sprite), layer, false);
            return;
        }

        _sprite.LayerSetVisible((ent, sprite), layer, true);
        _sprite.LayerSetRsiState((ent, sprite), layer, state);
    }

    private bool TryGetMoodLayer(Entity<MoodVisualsComponent, SpriteComponent> ent, out int layer)
    {
        var moodVisuals = ent.Comp1;
        var sprite = ent.Comp2;
        layer = default;

        if (!TryComp<HumanoidAppearanceComponent>(ent, out var humanoid))
            return false;

        if (!humanoid.MarkingSet.Markings.TryGetValue(moodVisuals.MarkingCategory, out var markings))
            return false;

        foreach (var marking in markings)
        {
            if (!moodVisuals.MoodMarkings.Contains(marking.MarkingId))
                continue;

            if (!_marking.TryGetMarking(marking, out var prototype))
                continue;

            if (TryGetMarkingLayer((ent, sprite), prototype, out layer))
                return true;
        }

        return false;
    }

    private bool TryGetMarkingLayer(Entity<SpriteComponent?> ent, MarkingPrototype prototype, out int layer)
    {
        layer = default;

        foreach (var markingSprite in prototype.Sprites)
        {
            if (markingSprite is not SpriteSpecifier.Rsi rsi)
                continue;

            var layerKey = $"{prototype.ID}-{rsi.RsiState}";
            if (_sprite.LayerMapTryGet(ent, layerKey, out layer, false))
                return true;
        }

        return false;
    }
}
