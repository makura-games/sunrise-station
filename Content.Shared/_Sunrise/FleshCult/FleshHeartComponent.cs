using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared.Flesh
{
    [RegisterComponent]
    public sealed partial class FleshHeartComponent : Component
    {
        [DataField("transformSound")] public SoundSpecifier TransformSound = new SoundCollectionSpecifier("gib");

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("entryDelay")]
        public float EntryDelay = 10f;

        public Container BodyContainer = default!;

        public EntityUid? AmbientAudioStream = default;

        [DataField("bodyToFinalStage"), ViewVariables(VVAccess.ReadWrite)]
        public int BodyToFinalStage = 3; // default 3

        [DataField("timeLiveFinalHeartToWin"), ViewVariables(VVAccess.ReadWrite)]
        public int TimeLiveFinalHeartToWin = 600; // default 600

        [DataField("spawnObjectsFrequency"), ViewVariables(VVAccess.ReadWrite)]
        public float SpawnObjectsFrequency = 60;

        [DataField("spawnObjectsAmount"), ViewVariables(VVAccess.ReadWrite)]
        public int SpawnObjectsAmount = 6;

        [DataField("spawnObjectsRadius"), ViewVariables(VVAccess.ReadWrite)]
        public float SpawnObjectsRadius = 5;

        [DataField, ViewVariables(VVAccess.ReadWrite)]
        public EntProtoId FleshTileId = "Flesh";

        [DataField("spawns"), ViewVariables(VVAccess.ReadWrite)]
        public Dictionary<string, float> Spawns = new();

        [DataField("spawnMobsFrequency"), ViewVariables(VVAccess.ReadWrite)]
        public float SpawnMobsFrequency = 100;

        [DataField("spawnMobsAmount"), ViewVariables(VVAccess.ReadWrite)]
        public int SpawnMobsAmount = 10;

        [DataField("spawnMobsRadius"), ViewVariables(VVAccess.ReadWrite)]
        public float SpawnMobsRadius = 3;

        [ViewVariables]
        public float Accumulator = 0;

        [ViewVariables]
        public float SpawnMobsAccumulator = 110;

        [ViewVariables]
        public float SpawnObjectsAccumulator = 0;

        [ViewVariables]
        public float FinalStageAccumulator = 0;

        [ViewVariables]
        public HeartStatus Status = HeartStatus.Base;

        public readonly HashSet<EntityUid> EdgeMobs = new();

        [DataField("damageMobsIfHeartDestruct", required: true)]
        [ViewVariables(VVAccess.ReadWrite)]
        public DamageSpecifier DamageMobsIfHeartDestruct = default!;

        [DataField("finalState")]
        public string? FinalState = "underpowered";
    }
}

public enum HeartStatus
{
    Base,
    Active,
    Disable
}
