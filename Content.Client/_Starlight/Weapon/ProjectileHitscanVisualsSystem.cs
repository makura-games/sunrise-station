using Content.Shared.Weapons.Hitscan.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.Client._Starlight.Weapon;

public sealed partial class ProjectileHitscanVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _spriteSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HitscanBasicVisualsComponent, ComponentInit>(OnVisualsInit);
    }

    private void OnVisualsInit(EntityUid uid, HitscanBasicVisualsComponent component, ref ComponentInit args)
    {
        if (component.Bullet == null)
            return;

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        // Set the sprite specifier on the base layer (layer 0)
        _spriteSystem.LayerSetSprite((uid, sprite), 0, component.Bullet.Sprite);
        _spriteSystem.LayerSetColor((uid, sprite), 0, component.Bullet.SpriteColor);
        _spriteSystem.LayerSetScale((uid, sprite), 0, component.Bullet.SpriteScale);

        // Also enable rotation if configured
        sprite.NoRotation = !component.Bullet.SpriteRotation;
    }
}
