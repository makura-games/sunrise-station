using Content.Client._Sunrise.Animations;
using Robust.Client.GameObjects;

#pragma warning disable IDE0130
namespace Content.Client.Rotation;

public sealed partial class RotationVisualizerSystem
{
    [Dependency] private SpritePoseSystem _pose = default!;

    /// <summary>
    /// updates the base pose over animationTime seconds, preserving an active pose override
    /// </summary>
    public void AnimateSpriteRotation(EntityUid uid, SpriteComponent spriteComp, Angle rotation, float animationTime)
    {
        _pose.SetBasePose((uid, spriteComp), rotation, animationTime);
    }
}
