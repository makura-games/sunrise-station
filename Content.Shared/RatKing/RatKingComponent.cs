using Content.Shared.Dataset;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.RatKing;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedRatKingSystem))]
[AutoGenerateComponentState]
public sealed partial class RatKingComponent : Component
{
    [DataField]
    public EntProtoId ActionRaiseArmy = "ActionRatKingRaiseArmy";

    /// <summary>
    ///     The action for the Raise Army ability
    /// </summary>
    [DataField]
    public EntityUid? ActionRaiseArmyEntity;

    // Sunrise-Start
    [DataField]
    public EntProtoId ActionRaiseGuard = "ActionRatKingRaiseGuard";

    /// <summary>
    ///     The action for the Raise Guard ability
    /// </summary>
    [DataField]
    public EntityUid? ActionRaiseGuardEntity;

    /// <summary>
    ///     The amount of hunger one use of Raise Guard consumes
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField(required: true)]
    public float HungerPerGuardUse = 75f;

    /// <summary>
    ///     The entity prototype of the mob that Raise Guard summons
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public EntProtoId GuardMobSpawnId = "MobRatGuard";
    // Sunrise-End

    /// <summary>
    ///     The amount of hunger one use of Raise Army consumes
    /// </summary>
    [DataField(required: true)]
    public float HungerPerArmyUse = 25f;

    /// <summary>
    ///     The entity prototype of the mob that Raise Army summons
    /// </summary>
    [DataField]
    public EntProtoId ArmyMobSpawnId = "MobRatServant";

    [DataField]
    public EntProtoId ActionDomain = "ActionRatKingDomain";

    /// <summary>
    ///     The action for the Domain ability
    /// </summary>
    [DataField]
    public EntityUid? ActionDomainEntity;

    /// <summary>
    ///     The amount of hunger one use of Domain consumes
    /// </summary>
    [DataField(required: true)]
    public float HungerPerDomainUse = 50f;

    /// <summary>
    ///     How many moles of ammonia are released after one us of Domain
    /// </summary>
    [DataField]
    public float MolesAmmoniaPerDomain = 200f;

    /// <summary>
    /// The current order that the Rat King assigned.
    /// </summary>
    [DataField("currentOrders")]
    [AutoNetworkedField]
    public RatKingOrderType CurrentOrder = RatKingOrderType.Follow;

    /// <summary>
    /// The servants that the rat king is currently controlling
    /// </summary>
    [DataField]
    public HashSet<EntityUid> Servants = new();

    // what the FUCK is a list :eagle:
    [DataField]
    public EntProtoId ActionOrderStay = "ActionRatKingOrderStay";

    [DataField]
    public EntityUid? ActionOrderStayEntity;

    [DataField]
    public EntProtoId ActionOrderFollow = "ActionRatKingOrderFollow";

    [DataField]
    public EntityUid? ActionOrderFollowEntity;

    [DataField]
    public EntProtoId ActionOrderCheeseEm = "ActionRatKingOrderCheeseEm";

    [DataField]
    public EntityUid? ActionOrderCheeseEmEntity;

    [DataField]
    public EntProtoId ActionOrderLoose = "ActionRatKingOrderLoose";

    [DataField]
    public EntityUid? ActionOrderLooseEntity;

    /// <summary>
    /// A dictionary with an order type to the corresponding callout dataset.
    /// </summary>
    [DataField]
    public Dictionary<RatKingOrderType, ProtoId<LocalizedDatasetPrototype>> OrderCallouts = new()
    {
        { RatKingOrderType.Stay, "RatKingCommandStay" },
        { RatKingOrderType.Follow, "RatKingCommandFollow" },
        { RatKingOrderType.CheeseEm, "RatKingCommandCheeseEm" },
        { RatKingOrderType.Loose, "RatKingCommandLoose" }
    };

    // Sunrise-Start
    [DataField]
    public int MaxArmyCount = 50;

    [DataField]
    public int MaxGuardCount = 4;

    /// <summary>
    /// The guards that the rat king is currently controlling
    /// </summary>
    [DataField("guards")]
    public HashSet<EntityUid> Guards = new();
    // Sunrise-End
}

[Serializable, NetSerializable]
public enum RatKingOrderType : byte
{
    Stay,
    Follow,
    CheeseEm,
    Loose
}
