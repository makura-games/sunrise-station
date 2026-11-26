using Content.Shared.Nutrition.AnimalHusbandry;
using Robust.Client.GameObjects;
using Content.Client._Sunrise.Animations.SpriteAnimation.Systems;

namespace Content.Client.Nutrition.EntitySystems;

/// <summary>
/// This handles visuals for <see cref="InfantComponent"/>
/// </summary>
public sealed partial class InfantSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SpriteAnimationSystem _spriteAnimation = default!; // Sunrise-Edit

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<InfantComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<InfantComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(EntityUid uid, InfantComponent component, ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        // Sunrise edit start - сохраняем базу без временных множителей
        component.DefaultScale = _spriteAnimation.GetBaseScale((uid, sprite));
        _spriteAnimation.SetBaseScale((uid, sprite), component.VisualScale);
        // Sunrise edit end
    }

    private void OnShutdown(EntityUid uid, InfantComponent component, ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        _spriteAnimation.SetBaseScale((uid, sprite), component.DefaultScale); // Sunrise-Edit
    }
}
