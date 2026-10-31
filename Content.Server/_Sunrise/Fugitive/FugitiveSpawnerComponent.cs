using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Fugitive
{
    [RegisterComponent]
    public sealed partial class FugitiveSpawnerComponent : Component
    {
        [DataField("spawnSound")]
        public SoundSpecifier SpawnSoundPath = new SoundPathSpecifier("/Audio/Effects/clang.ogg");

        [ViewVariables(VVAccess.ReadWrite), DataField]
        public EntProtoId Prototype = "MobHumanFugitive";

        public List<string> Implants = new() { "UplinkImplant", "FreedomImplant"};
    }
}
