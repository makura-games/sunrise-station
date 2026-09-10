using Content.Client._Sunrise.BloodCult;
using Content.Client.Body;
using Content.Shared._Sunrise.Abilities.Milira;
using Content.Shared._Sunrise.Humanoid;
using Content.Shared._Sunrise.Mood;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Sunrise.Mood;

/// <summary>
/// Применяет состояния настроения к настроенным слоям markings гуманоида.
/// </summary>
public sealed class MoodVisualizerSystem : VisualizerSystem<MoodVisualsComponent>
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SunriseHumanoidBodySystem _body = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MoodVisualsComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<MoodVisualsComponent, SunriseMarkingsUpdatedEvent>(OnMarkingsUpdated);
        SubscribeLocalEvent<MoodVisualsComponent, HumanoidLayerVisibilityChangedEvent>(OnLayerVisibilityChanged,
            after: [typeof(BodySystem)]);
    }

    private void OnStartup(Entity<MoodVisualsComponent> ent, ref ComponentStartup args)
        => UpdateAppearance(ent);

    protected override void OnAppearanceChange(EntityUid uid, MoodVisualsComponent component, ref AppearanceChangeEvent args)
        => UpdateAppearance((uid, component), args.Component, args.Sprite);

    private void OnMarkingsUpdated(Entity<MoodVisualsComponent> ent, ref SunriseMarkingsUpdatedEvent args)
        => UpdateAppearance(ent);

    private void OnLayerVisibilityChanged(Entity<MoodVisualsComponent> ent, ref HumanoidLayerVisibilityChangedEvent args)
        => UpdateAppearance(ent);

    private void UpdateAppearance(
        Entity<MoodVisualsComponent> ent,
        AppearanceComponent? appearance = null,
        SpriteComponent? sprite = null)
    {
        if (!Resolve(ent.Owner, ref appearance, ref sprite, false))
            return;

        var visible = TryGetMoodState(ent, appearance, out var state);
        UpdateMoodMarking(ent, sprite, visible, state);
    }

    private bool TryGetMoodState(
        Entity<MoodVisualsComponent> ent,
        AppearanceComponent appearance,
        out string? state)
    {
        state = null;

        if (HasComp<PentagramComponent>(ent) && HasComp<WingToggleComponent>(ent))
            return false;

        return !AppearanceSystem.TryGetData<MoodThreshold>(ent, MoodVisuals.CurrentMoodThreshold, out var moodThreshold, appearance) ? ent.Comp.VisibleWithoutMood : ent.Comp.MoodStates.TryGetValue(moodThreshold, out state);
    }

    private void UpdateMoodMarking(
        Entity<MoodVisualsComponent> ent,
        SpriteComponent sprite,
        bool moodVisible,
        string? state)
    {
        if (!_prototype.TryIndex(ent.Comp.Marking, out var prototype))
            return;

        Entity<SpriteComponent> spriteEnt = (ent, sprite);
        var nullableSpriteEnt = spriteEnt.AsNullable();
        var visible = moodVisible && _body.IsLayerVisible(ent, prototype.BodyPart);
        foreach (var markingSprite in prototype.Sprites)
        {
            if (markingSprite is not SpriteSpecifier.Rsi rsi)
                continue;

            var layerKey = $"{prototype.ID}-{rsi.RsiState}";
            if (!SpriteSystem.LayerMapTryGet(nullableSpriteEnt, layerKey, out var layer, false))
                continue;

            sprite.LayerSetShader(layer, "unshaded");
            SpriteSystem.LayerSetVisible(nullableSpriteEnt, layer, visible);
            SpriteSystem.LayerSetRsiState(nullableSpriteEnt, layer, state ?? rsi.RsiState);
        }
    }
}
