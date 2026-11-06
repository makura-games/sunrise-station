using Content.Client._Sunrise.BloodCult;
using Content.Shared._Sunrise.Abilities.Milira;
using Content.Shared._Sunrise.Mood;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._Sunrise.Mood;

/// <summary>
/// Обрабатывает отображение эффектов настроения на сущностях с компонентом настроения.
/// </summary>
public sealed partial class MoodVisualizerSystem : VisualizerSystem<MoodVisualsComponent>
{
    [Dependency] private SpriteSystem _spriteSystem = default!;
    [Dependency] private SharedAppearanceSystem _appearanceSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MoodVisualsComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<MoodVisualsComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<MoodVisualsComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(ent.Owner, out var sprite))
            return;

        if (_spriteSystem.LayerMapTryGet((ent.Owner, sprite), MoodVisualLayers.Mood, out var layer, false))
            _spriteSystem.RemoveLayer((ent.Owner, sprite), layer);
    }

    private void OnComponentInit(Entity<MoodVisualsComponent> ent, ref ComponentInit args)
    {
        if (!TryComp<SpriteComponent>(ent.Owner, out var sprite))
            return;

        _spriteSystem.LayerMapReserve((ent.Owner, sprite), MoodVisualLayers.Mood);
        _spriteSystem.LayerSetVisible((ent.Owner, sprite), MoodVisualLayers.Mood, false);
        sprite.LayerSetShader(MoodVisualLayers.Mood, "unshaded");
        if (ent.Comp.Sprite != null)
            _spriteSystem.LayerSetSprite((ent.Owner, sprite), MoodVisualLayers.Mood, ent.Comp.Sprite);

        if (TryComp<AppearanceComponent>(ent.Owner, out var appearance))
            UpdateAppearance(ent, sprite, appearance);
    }

    protected override void OnAppearanceChange(EntityUid uid, MoodVisualsComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite != null)
            UpdateAppearance((uid, component), args.Sprite, args.Component);
    }

    private bool ShouldHideMoodVisuals(Entity<MoodVisualsComponent> ent)
    {
        return HasComp<PentagramComponent>(ent) && HasComp<WingToggleComponent>(ent);
    }

    private void UpdateAppearance(Entity<MoodVisualsComponent> ent, SpriteComponent sprite, AppearanceComponent appearance)
    {
        if (!_spriteSystem.LayerMapTryGet((ent, sprite), MoodVisualLayers.Mood, out var index, false))
            return;

        if (ShouldHideMoodVisuals(ent))
        {
            _spriteSystem.LayerSetVisible((ent, sprite), index, false);
            return;
        }

        if (!_appearanceSystem.TryGetData<MoodThreshold>(ent.Owner, MoodVisuals.CurrentMoodThreshold, out var moodThreshold, appearance))
        {
            _spriteSystem.LayerSetVisible((ent.Owner, sprite), index, false);
            return;
        }

        // Проверяем, есть ли состояние спрайта для этого порога настроения
        if (!ent.Comp.MoodStates.TryGetValue(moodThreshold, out var state))
        {
            _spriteSystem.LayerSetVisible((ent.Owner, sprite), index, false);
            return;
        }

        // Показываем слой спрайта и устанавливаем состояние
        _spriteSystem.LayerSetVisible((ent.Owner, sprite), index, true);
        _spriteSystem.LayerSetRsiState((ent.Owner, sprite), index, state);
    }
}

public enum MoodVisualLayers : byte
{
    Mood
}
