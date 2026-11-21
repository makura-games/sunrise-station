using Content.Shared.Alert;
using Content.Shared.FixedPoint;
using Content.Shared.StatusIcon;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.FleshCult;

[RegisterComponent, NetworkedComponent]
public sealed partial class FleshCultistComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)] public FixedPoint2 Hunger = 100;

    [ViewVariables(VVAccess.ReadWrite), DataField("hungerСonsumption")]
    public FixedPoint2 HungerСonsumption = -0.0555555555556; // 100 hunger in 30 minutes

    [ViewVariables(VVAccess.ReadWrite), DataField("maxHunger")]
    public FixedPoint2 MaxHunger = 300;

    [DataField]
    public EntProtoId ActionFleshCultistShop = "FleshCultistShop";

    [DataField]
    public FixedPoint2 StartingMutationPoints = 5;

    [DataField]
    public EntityUid? ActionFleshCultistShopEntity;

    [ViewVariables(VVAccess.ReadWrite), DataField]
    public EntProtoId FleshMutationMobId = "MobFleshPudge";

    public SoundSpecifier BuySuccesSound = new SoundPathSpecifier(
        "/Audio/_Sunrise/FleshCult/flesh_cultist_buy_succes.ogg");

    [ViewVariables] public float Accumulator = 0;

    [ViewVariables] public float AccumulatorStarveNotify = 0;

    [DataField("fleshStatusIcon")]
    public ProtoId<FactionIconPrototype> StatusIcon { get; set; } = "FleshFaction";

    [DataField]
    public ProtoId<AlertPrototype> MutationPointAlert = "MutationPoint";

    [DataField]
    public SoundSpecifier SoundMutation = new SoundPathSpecifier("/Audio/_Sunrise/FleshCult/flesh_cultist_mutation.ogg");

    /// <summary>
    /// Flag to indicate that the entity is already in the process of dying/gibbing.
    /// </summary>
    public bool IsDeathPending = false;

    /// <summary>
    /// Flag to indicate that the entity is scheduled for transformation.
    /// </summary>
    [ViewVariables]
    public bool IsTransformationPending = false;
}
