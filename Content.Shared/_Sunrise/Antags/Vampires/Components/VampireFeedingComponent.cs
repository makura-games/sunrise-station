using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Antags.Vampires.Components;

/// <summary>
/// Питание и история укусов, синхронизируемые владельцу.
/// </summary>
// Полный снимок обходит ограничение генератора дельт состояния для Dictionary с ключами EntityUid.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VampireFeedingComponent : Component
{
    public override bool SendOnlyToOwner => true;

    /// <summary>
    /// Накопленная кровь для прогрессии и целей, без ограничения по виду жертвы.
    /// </summary>
    [AutoNetworkedField]
    public int TotalBlood;

    /// <summary>
    /// Расход запаса крови в секунду при нулевой сытости.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int StarvationDrunkBloodDrainPerSecond = 2;

    /// <summary>
    /// Интервал проверки голода и святых уязвимостей.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan UpdateDelay = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public SoundSpecifier BiteSound = new SoundPathSpecifier("/Audio/Effects/bite.ogg");

    [DataField, AutoNetworkedField]
    public EntProtoId BiteEffect = "WeaponArcBite";

    /// <summary>
    /// Слоты, способные закрыть рот.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string[] MouthCoveringSlots = ["mask", "head"];

    /// <summary>
    /// Громкость звука укуса в децибелах.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float BiteVolume = -7f;

    /// <summary>
    /// Число глотков с одной цели до повреждения её глаз.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int BitesPerEyeDamage = 3;

    /// <summary>
    /// Повреждение глаз каждые BitesPerEyeDamage глотков.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int EyeDamage = 1;

    /// <summary>
    /// ID текущего DoAfter питания.
    /// </summary>
    [AutoNetworkedField]
    public ushort? DrinkDoAfter;

    /// <summary>
    /// Полученная кровь по целям; удалённые источники исключаются.
    /// </summary>
    [AutoNetworkedField]
    public Dictionary<EntityUid, float> BloodDrunkFromTargets = [];

    /// <summary>
    /// Глотки по целям с момента последнего повреждения глаз.
    /// </summary>
    [AutoNetworkedField]
    public Dictionary<EntityUid, int> BiteCountsByTarget = [];

    /// <summary>
    /// Дробный остаток пополнения DrunkBlood.
    /// </summary>
    [AutoNetworkedField]
    public float DrunkBloodRemainder;

    /// <summary>
    /// Дробный остаток пополнения TotalBlood.
    /// </summary>
    [AutoNetworkedField]
    public float TotalBloodRemainder;

    /// <summary>
    /// Число учтённых жертв для святых уязвимостей; убийство не требуется.
    /// </summary>
    [AutoNetworkedField]
    public int UniqueVictims;
}
