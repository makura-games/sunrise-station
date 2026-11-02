using Content.Shared.Damage;
using Robust.Shared.Prototypes;

#pragma warning disable IDE0130 // Namespace не соответствует расположению файла
namespace Content.Shared.Zombies;

public sealed partial class ZombieComponent
{
    /// <summary>
    /// Действие прыжка зомби.
    /// </summary>
    [DataField]
    public EntProtoId JumpAction = "ZombieJump";

    /// <summary>
    /// Действие поиска ближайшего выжившего.
    /// </summary>
    [DataField]
    public EntProtoId FlairAction = "ZombieFlair";

    /// <summary>
    /// Длительность паралича после попадания брошенным зомби.
    /// </summary>
    [DataField]
    public TimeSpan ParalyzeTime = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Урон после попадания брошенным зомби.
    /// </summary>
    [DataField]
    public DamageSpecifier ThrowDamage = new()
    {
        DamageDict = new()
        {
            { "Slash", 15 },
        },
    };

    /// <summary>
    /// Максимальная дистанция прыжка.
    /// </summary>
    [DataField]
    public float MaxJumpDistance = 10f;

    /// <summary>
    /// Максимальная дистанция поиска выжившего.
    /// </summary>
    [DataField]
    public float MaxFlairDistance = 500f;
}
