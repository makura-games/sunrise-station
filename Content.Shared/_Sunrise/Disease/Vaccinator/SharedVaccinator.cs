using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Disease.Vaccinator;

/// <summary>
/// Ключ интерфейса вакцинатора.
/// </summary>
[Serializable, NetSerializable]
public enum VaccinatorUiKey : byte
{
    Key,
}

/// <summary>
/// Что машина видит в слоте крови. Нужно, чтобы интерфейс объяснял, почему сыворотку не выделить.
/// </summary>
[Serializable, NetSerializable]
public enum DiseaseSerumStatus : byte
{
    /// <summary>
    /// Ёмкости в слоте нет.
    /// </summary>
    NoContainer,

    /// <summary>
    /// Ёмкость есть, но крови в ней нет.
    /// </summary>
    NoBlood,

    /// <summary>
    /// Кровь есть, но её меньше, чем нужно на одну дозу.
    /// </summary>
    NotEnoughBlood,

    /// <summary>
    /// Кровь есть, но её донор не перенёс ни одной болезни.
    /// </summary>
    UnknownDonor,

    /// <summary>
    /// Кровь годится: донор известен и перенёс хотя бы одну болезнь.
    /// </summary>
    Ready,
}

/// <summary>
/// Запрос на синтез вакцины из выбранного антигена.
/// </summary>
[Serializable, NetSerializable]
public sealed class VaccinatorSynthesizeMessage(int antigenIndex) : BoundUserInterfaceMessage
{
    /// <summary>
    /// Индекс партии в <see cref="Components.VaccinatorComponent.Candidates"/>.
    /// </summary>
    public readonly int AntigenIndex = antigenIndex;
}

/// <summary>
/// Запрос на изготовление дозы по уже найденному рецепту.
/// </summary>
[Serializable, NetSerializable]
public sealed class VaccinatorProduceMessage : BoundUserInterfaceMessage;

/// <summary>
/// Запрос на выделение сыворотки из крови переболевшего донора.
/// </summary>
[Serializable, NetSerializable]
public sealed class VaccinatorExtractSerumMessage(int optionIndex) : BoundUserInterfaceMessage
{
    /// <summary>
    /// Индекс штамма в <see cref="Components.VaccinatorComponent.SerumOptions"/>.
    /// </summary>
    public readonly int OptionIndex = optionIndex;
}
