using System.Numerics;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Antags.Vampires.Components.Classes;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GargantuaComponent : VampireClassComponent
{
    /// <summary>
    ///     Активен ли переключатель Неодолимой силы
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool OverwhelmingForceActive;

    /// <summary>
    ///     Заряжает ли вампир рывок сейчас
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsCharging;

    /// <summary>
    /// Текущее направление рывка вектором.
    /// </summary>
    public Vector2 ChargeDirectionVector;

    /// <summary>
    /// Скорость движения во время рывка.
    /// </summary>
    public float ChargeSpeed;

    /// <summary>
    /// Урон существам при столкновении с рывком.
    /// </summary>
    public float ChargeCreatureDamage;

    /// <summary>
    /// Дистанция отбрасывания существа при столкновении с рывком.
    /// </summary>
    public float ChargeCreatureThrowDistance;

    /// <summary>
    /// Зарезервированный урон структурам при столкновении с рывком.
    /// </summary>
    public float ChargeStructuralDamage;

    /// <summary>
    /// Звук столкновения при рывке.
    /// </summary>
    public SoundSpecifier? ChargeSound;

    /// <summary>
    /// Кулдаун всплывающего уведомления о запрете стрельбы при Кровавом вспучивании.
    /// </summary>
    [DataField]
    public TimeSpan BloodSwellShootPopupCooldown = TimeSpan.FromSeconds(1f);
    /// <summary>
    /// Время следующего допустимого уведомления о запрете стрельбы.
    /// </summary>
    [DataField]
    public TimeSpan? BloodSwellShootNextPopupTime;

    /// <summary>
    /// Последнее оружие, которым пытался выстрелить вампир при Кровавом вспучивании.
    /// </summary>
    [DataField]
    public EntityUid? BloodSwellShootLastGun;

    /// <summary>
    /// Порог TotalBlood, после которого питьё крови лечит Гаргантую.
    /// </summary>
    [DataField]
    public int PassiveHealBloodThreshold = 300;

    /// <summary>
    /// Группы урона, восстанавливаемые Гаргантую при питье крови.
    /// </summary>
    [DataField]
    public Dictionary<string, FixedPoint2> PassiveHealGroups = new()
    {
        { "Brute", FixedPoint2.New(3) },
        { "Burn", FixedPoint2.New(3) },
    };

    /// <summary>
    /// Множитель скорости взлома дверей при Неодолимой силе.
    /// </summary>
    [DataField]
    public float OverwhelmingForcePrySpeedModifier = 10f;

    /// <summary>
    /// Стоимость крови за взлом двери при Неодолимой силе.
    /// </summary>
    [DataField]
    public int OverwhelmingForceDoorPryBloodCost = 15;

    /// <summary>
    /// Звук взлома двери при Неодолимой силе.
    /// </summary>
    [DataField]
    public SoundSpecifier OverwhelmingForcePrySound = new SoundPathSpecifier("/Audio/Items/crowbar.ogg");
}
