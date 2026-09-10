using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Camera;

/// <summary>
/// Хранит активные эффекты плавной тряски камеры владельца сущности.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SunriseScreenShakeComponent : Component
{
    /// <summary>
    /// Активные команды тряски, синхронизируемые только владельцу сущности.
    /// </summary>
    [AutoNetworkedField]
    public HashSet<SunriseScreenShakeCommand> Commands = [];

    public override bool SendOnlyToOwner => true;
}

/// <summary>
/// Одна команда тряски с рассчитанным интервалом жизни.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public sealed partial record SunriseScreenShakeCommand(
    SunriseScreenShakeParameters? Translational,
    SunriseScreenShakeParameters? Rotational,
    TimeSpan Start,
    TimeSpan CalculatedEnd);

/// <summary>
/// Параметры шумовой тряски и её квадратичного затухания.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial record SunriseScreenShakeParameters
{
    /// <summary>
    /// Начальная сила тряски.
    /// </summary>
    [DataField(required: true)]
    public float Trauma;

    /// <summary>
    /// Скорость квадратичного затухания.
    /// </summary>
    [DataField]
    public float DecayRate = 1.2f;

    /// <summary>
    /// Частота шума, определяющая резкость движения камеры.
    /// </summary>
    [DataField]
    public float Frequency = 0.01f;
}
