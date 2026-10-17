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

    /// <summary>
    /// server CurTime at animation start, used by clients to restore its phase in synchronized game time
    /// </summary>
    public TimeSpan StartedAt;

    [Serializable, NetSerializable]
    public sealed partial class EmoteAnimationComponentState : ComponentState
    {
        public string AnimationId { get; init; }
        public TimeSpan StartedAt { get; init; }

        public EmoteAnimationComponentState(string animationId, TimeSpan startedAt)
        {
            AnimationId = animationId;
            StartedAt = startedAt;
        }
    }
}

public sealed partial class EmoteActionEvent : InstantActionEvent
{
    [ViewVariables, DataField("emote", readOnly: true, required: true)]
    public string Emote = default!;
};
