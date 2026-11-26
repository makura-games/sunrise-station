using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Animations;

[Serializable, NetSerializable]
public sealed partial class EmoteAnimationComponentState : ComponentState
{
    public string AnimationId { get; init; }

    public EmoteAnimationComponentState(string animationId)
    {
        AnimationId = animationId;
    }
}
