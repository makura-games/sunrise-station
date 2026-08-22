using Content.Shared._Sunrise.ScaleSprite;
using System.Numerics;

namespace Content.Server._Sunrise.ScaleSprite;

public sealed partial class ScaleSpriteSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ScaleSpriteComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<RescaleSpriteComponent, ComponentAdd>(OnComponentInit);
    }

    private void OnComponentInit(EntityUid uid, ScaleSpriteComponent component, ComponentInit args)
    {
        var appearance = EnsureComp<AppearanceComponent>(uid);
        _appearance.SetData(uid, ScaleSpriteVisuals.OldScale, component.Scale, appearance);
        _appearance.SetData(uid, ScaleSpriteVisuals.Scale, component.Scale, appearance);
        Dirty(uid, component);
    }

    private void OnComponentInit(EntityUid uid, RescaleSpriteComponent component, ComponentAdd args)
    {
        var appearance = EnsureComp<AppearanceComponent>(uid);
        var scaleSpriteComponent = EnsureComp<ScaleSpriteComponent>(uid);
        if (!_appearance.TryGetData<Vector2>(uid, ScaleSpriteVisuals.OldScale, out var oldScale, appearance))
            oldScale = Vector2.One;
        _appearance.SetData(uid, ScaleSpriteVisuals.Scale, oldScale * component.Scale, appearance);
        Dirty(uid, scaleSpriteComponent);
        RemComp<RescaleSpriteComponent>(uid);
    }

    public void Scale(EntityUid uid, Vector2 scale)
    {
        var appearance = EnsureComp<AppearanceComponent>(uid);
        var component = EnsureComp<ScaleSpriteComponent>(uid);
        _appearance.SetData(uid, ScaleSpriteVisuals.Scale, scale, appearance);
        Dirty(uid, component);
    }
}
