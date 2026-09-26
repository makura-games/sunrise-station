using System.Collections.Generic;
using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.CarpQueen;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedCarpQueenSystem), typeof(CarpQueenAccessSystem))]
[AutoGenerateComponentState]
public sealed partial class CarpQueenComponent : Component
{
    [DataField]
    public EntProtoId ActionSummon = "ActionCarpQueenSummon";

    [DataField("actionSummonEntity")]
    public EntityUid? ActionSummonEntity;

    [DataField]
    public EntProtoId ActionOrderStay = "ActionCarpQueenOrderStay";

    [DataField("actionOrderStayEntity")]
    public EntityUid? ActionOrderStayEntity;

    [DataField]
    public EntProtoId ActionOrderFollow = "ActionCarpQueenOrderFollow";

    [DataField("actionOrderFollowEntity")]
    public EntityUid? ActionOrderFollowEntity;

    [DataField]
    public EntProtoId ActionOrderKill = "ActionCarpQueenOrderKill";

    [DataField("actionOrderKillEntity")]
    public EntityUid? ActionOrderKillEntity;

    [DataField]
    public EntProtoId ActionOrderLoose = "ActionCarpQueenOrderLoose";

    [DataField("actionOrderLooseEntity")]
    public EntityUid? ActionOrderLooseEntity;

    /// <summary>
    /// Текущий приказ, примененный к слугам.
    /// </summary>
    [DataField("currentOrders"), AutoNetworkedField]
    public CarpQueenOrderType CurrentOrder = CarpQueenOrderType.Loose;

    /// <summary>
    /// Список созданных слуг под контролем королевы.
    /// </summary>
    [DataField("servants")]
    public HashSet<EntityUid> Servants = new();

    /// <summary>
    /// Активные яйца, созданные королевой и еще не вылупившиеся.
    /// </summary>
    [DataField("eggs")]
    public HashSet<EntityUid> Eggs = new();

    /// <summary>
    /// Пул ID прототипов карпов-слуг для случайного выбора при призыве.
    /// </summary>
    [DataField("armyMobSpawnOptions")]
    public List<string> ArmyMobSpawnOptions = new()
    {
        "MobCarpServantDungeon",
        "MobCarpServantMagic",
        "MobCarpServantHolo",
        "MobCarpServantRainbow",
        "MobCarpServantDragon"
    };

    /// <summary>
    /// Соответствие dataset для реплик приказов, произносимых при смене приказа.
    /// </summary>
    [DataField("orderCallouts")]
    public Dictionary<CarpQueenOrderType, string> OrderCallouts = new()
    {
        { CarpQueenOrderType.Stay, "CarpQueenCommandStay" },
        { CarpQueenOrderType.Follow, "CarpQueenCommandFollow" },
        { CarpQueenOrderType.Kill, "CarpQueenCommandKill" },
        { CarpQueenOrderType.Loose, "CarpQueenCommandLoose" }
    };

    /// <summary>
    /// Голод, расходуемый на одно использование призыва.
    /// </summary>
    [DataField("hungerPerSummon")]
    public float HungerPerSummon = 25f;

    /// <summary>
    /// Отслеживает последний замеченный голод для небольшого лечения при еде.
    /// Только на сервере, не синхронизируется по сети.
    /// </summary>
    public float LastObservedHunger;

    /// <summary>
    /// Максимальное суммарное количество слуг и яиц у королевы одновременно.
    /// </summary>
    [DataField("maxArmySize")]
    public int MaxArmySize = 5;

    /// <summary>
    /// HP, восстанавливаемое за 1 единицу полученного голода при еде.
    /// </summary>
    [DataField("healPerHunger")]
    public float HealPerHunger = 0.2f;

    /// <summary>
    /// Максимальное HP, восстанавливаемое за тик от еды.
    /// </summary>
    [DataField("maxHealPerTick")]
    public float MaxHealPerTick = 5f;

    /// <summary>
    /// Шансы спавна разных типов карпов при вылуплении из яйца.
    /// Ключ: ID прототипа, значение: шанс (0-100).
    /// Если сумма меньше 100, оставшийся шанс уходит в значение по умолчанию (MobCarpServantRainbow).
    /// </summary>
    [DataField("spawnChances")]
    public Dictionary<string, int> SpawnChances = new()
    {
        { "MobCarpServantRainbow", 80 },
        { "MobCarpServantHolo", 10 },
        { "MobCarpServantDungeon", 10 }
    };
}

