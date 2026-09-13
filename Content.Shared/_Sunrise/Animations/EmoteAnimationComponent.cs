using Content.Shared.Actions;
using Content.Shared.Chat.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Animations;

[RegisterComponent, NetworkedComponent]
public sealed partial class EmoteAnimationComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    public string AnimationId = "none";

    [Serializable, NetSerializable]
    public sealed partial class EmoteAnimationComponentState : ComponentState
    {
        public string AnimationId { get; init; }

        public EmoteAnimationComponentState(string animationId)
        {
            AnimationId = animationId;
        }
    }
}

public sealed partial class EmoteActionEvent : InstantActionEvent
{
    [ViewVariables, DataField("emote", readOnly: true, required: true)]
    public string Emote = default!;
};
