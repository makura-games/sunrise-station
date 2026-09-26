using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.StationGoal
{
    [Prototype]
    public sealed partial class StationGoalPrototype : IPrototype
    {
        [IdDataField]
        public string ID { get; private set; } = default!;

        [DataField("text")]
        public string Text { get; set; } = string.Empty;

        // Sunrise-start
        [ViewVariables(VVAccess.ReadWrite), DataField]
        public EntProtoId LockBoxPrototypeId = "LockboxCaptain";

        [ViewVariables(VVAccess.ReadOnly), DataField]
        public List<EntProtoId> ExtraItems = [];
        // Sunrise-end

        [DataField]
        public int? MinPlayers;

        [DataField]
        public int? MaxPlayers;
    }
}
