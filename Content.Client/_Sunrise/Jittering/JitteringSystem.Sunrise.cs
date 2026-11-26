using Content.Shared.Jittering;
using Robust.Client.GameObjects;

#pragma warning disable IDE0130
namespace Content.Client.Jittering;

public sealed partial class JitteringSystem
{
    private void PlayAnimation(EntityUid uid)
    {
        if (TryComp(uid, out JitteringComponent? jittering) && TryComp(uid, out SpriteComponent? sprite))
            _spriteAnimation.PlayOffset(uid, GetAnimation(jittering, sprite), _jitterAnimationKey,
                jittering.StartOffset, true);
    }
}
