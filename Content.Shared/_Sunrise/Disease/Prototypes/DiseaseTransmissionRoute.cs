using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Disease.Prototypes;

/// <summary>
/// Пути, которыми болезнь попадает в нового носителя.
/// Болезнь передаётся только теми путями, что перечислены в <see cref="DiseasePrototype.Routes"/>.
/// </summary>
[Flags, Serializable, NetSerializable]
public enum DiseaseTransmissionRoute : byte
{
    None = 0,

    /// <summary>
    /// Касание заражённого существа или загрязнённого предмета.
    /// </summary>
    Contact = 1 << 0,

    /// <summary>
    /// Поедание или питьё чего-то заражённого.
    /// </summary>
    Ingestion = 1 << 1,

    /// <summary>
    /// Капли, которые разлетаются вблизи при заметном симптоме вроде кашля.
    /// </summary>
    Droplet = 1 << 2,

    /// <summary>Укол напрямую в кровь.</summary>
    Injection = 1 << 3,
}
