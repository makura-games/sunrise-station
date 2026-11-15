using System.Numerics;
using Content.Shared.Sprite;
using Content.Client._Sunrise.Animations.SpriteAnimation.Systems;
using Robust.Client.GameObjects;

namespace Content.Client.Sprite;

public sealed partial class ScaleVisualsSystem : SharedScaleVisualsSystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SpriteAnimationSystem _spriteAnimation = default!; // Sunrise-Edit

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ScaleVisualsComponent, AppearanceChangeEvent>(OnChangeData);
    }

    private void OnChangeData(Entity<ScaleVisualsComponent> ent, ref AppearanceChangeEvent args)
    {
        if (!args.AppearanceData.TryGetValue(ScaleVisuals.Scale, out var scale) ||
            args.Sprite == null) return;

        // save the original scale
        ent.Comp.OriginalScale ??= _spriteAnimation.GetBaseScale((ent.Owner, args.Sprite)); // Sunrise-Edit

        var vecScale = (Vector2)scale;
        _spriteAnimation.SetBaseScale((ent.Owner, args.Sprite), vecScale); // Sunrise-Edit
    }

    // revert to the original scale
    protected override void ResetScale(Entity<ScaleVisualsComponent> ent)
    {
        base.ResetScale(ent);

        // Sunrise edit start - восстанавливаем базу без вкладов анимаций
        if (ent.Comp.OriginalScale != null && TryComp<SpriteComponent>(ent, out var sprite))
            _spriteAnimation.SetBaseScale((ent.Owner, sprite), ent.Comp.OriginalScale.Value);
        // Sunrise edit end
    }
}
