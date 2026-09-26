using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.FleshCult
{
    [RegisterComponent]
    public sealed partial class FleshAbilitiesComponent : Component
    {
        [DataField]
        public List<EntProtoId> StartingActions = [];

        public List<EntityUid> Actions = new();

        public EntProtoId ActionFleshCultistDevourId = "FleshCultistDevour";

        [ViewVariables(VVAccess.ReadWrite), DataField("bloodWhitelist")]
        public List<string> BloodWhitelist = new()
        {
            "Blood",
            "CopperBlood",
            "InsectBlood",
            "AmmoniaBlood",
            "ZombieBlood"
        };

        public float DevourTime = 10f;

        public SoundSpecifier DevourSound = new SoundPathSpecifier("/Audio/_Sunrise/FleshCult/devour_flesh_cultist.ogg");

        public Solution AdrenalinReagents = new()
        {
            Contents = { new ReagentQuantity(new ReagentId("Ephedrine", null), 10) }
        };

        public Solution HealDevourReagents = new()
        {
            Contents =
            {
                new ReagentQuantity(new ReagentId("Carol", null), 20),
            }
        };

        public Solution HealBloodAbsorbReagents = new()
        {
            Contents =
            {
                new ReagentQuantity(new ReagentId("Carol", null), 1),
            }
        };

        public SoundSpecifier BloodAbsorbSound = new SoundPathSpecifier("/Audio/Effects/Fluids/splat.ogg");

        public EntProtoId BulletAcidSpawnId = "BulletSplashAcid";

        public EntProtoId FleshHeartId = "FleshHeart";

        public EntProtoId HuggerMobSpawnId = "MobFleshHugger";

        public SoundSpecifier SoundBulletAcid = new SoundPathSpecifier("/Audio/_Sunrise/FleshCult/flesh_cultist_mutation.ogg");

        public SoundSpecifier SoundMutation = new SoundPathSpecifier("/Audio/_Sunrise/FleshCult/flesh_cultist_mutation.ogg");

        public SoundSpecifier? SoundThrowHugger = new SoundPathSpecifier("/Audio/_Sunrise/FleshCult/throw_worm.ogg");
    }
}
