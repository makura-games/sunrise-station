using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.CollectiveMind
{
    [RegisterComponent, NetworkedComponent]
    public sealed partial class CollectiveMindComponent : Component
    {
        [DataField]
        public List<ProtoId<CollectiveMindPrototype>> Minds = [];
    }
}
