using Content.Shared.Popups;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.NewLife
{
    public abstract partial class SharedNewLifeSystem : EntitySystem
    {
        [Dependency] protected SharedPopupSystem Popup = default!;

        public override void Initialize()
        {
            base.Initialize();
        }
    }

    [Serializable, NetSerializable]
    public sealed class NewLifeOpenRequest : EntityEventArgs
    {
    }


    [Serializable, NetSerializable]
    [DataDefinition]
    public sealed partial class NewLifeUserData
    {
        public TimeSpan NextAllowRespawn { get; set; }
        public List<int> UsedCharactersForRespawn { get; set; } = new();
    }
}
