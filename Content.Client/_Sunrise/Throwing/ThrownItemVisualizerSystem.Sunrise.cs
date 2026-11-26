using Content.Client._Sunrise.Animations.SpriteAnimation.Systems;
using Content.Shared.Throwing;
using Robust.Client.GameObjects;

#pragma warning disable IDE0130
namespace Content.Client.Throwing;

public sealed partial class ThrownItemVisualizerSystem
{
    [Dependency] private SpriteAnimationSystem _spriteAnimation = default!;

    private void ApplyThrowAnimation(Entity<ThrownItemComponent> ent)
    {
        if (!ent.Comp.Animate)
        {
            _spriteAnimation.Stop(ent.Owner, AnimationKey);
            return;
        }

        if (_spriteAnimation.IsPlaying(ent.Owner, AnimationKey))
            return;

        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var animation = GetAnimation((ent.Owner, ent.Comp, sprite));
        if (animation == null)
            return;

        _spriteAnimation.PlayScale(ent.Owner, animation, AnimationKey);
    }
}
