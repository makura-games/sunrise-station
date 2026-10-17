using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Abilities.Jump
{
    [RegisterComponent]
    public sealed partial class JumpSkillComponent : Component
    {
        [DataField]
        public EntProtoId ActionJumpId = "Jump";

        [DataField]
        public float ThrowSpeed = 7F;

        [DataField]
        public float ThrowRange = 5F;

        [DataField]
        public float MaxThrow = 5f;
    }
}
