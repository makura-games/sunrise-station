using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Disease.Prototypes;

/// <summary>
/// Чем именно произошёл контакт при контактной передаче.
/// </summary>
/// <remarks>
/// Нужен экипировке, которая закрывает только часть тела: перчатки спасают от касания руками,
/// но не от укуса в ногу и не от наростов на полу. В YAML пишется списком: <c>contactKinds: [ Bite, Step ]</c>.
/// </remarks>
[Flags, Serializable, NetSerializable]
public enum DiseaseContactKind : byte
{
    None = 0,

    /// <summary>
    /// Касание руками: взять заражённый предмет, обнять больного, потрогать труп.
    /// </summary>
    Touch = 1 << 0,

    /// <summary>
    /// Шаг по заражённому наросту на полу.
    /// </summary>
    Step = 1 << 1,

    /// <summary>
    /// Пробитая кожа: укус, когти или заражённый клинок.
    /// </summary>
    Bite = 1 << 2,

    All = Touch | Step | Bite,
}
