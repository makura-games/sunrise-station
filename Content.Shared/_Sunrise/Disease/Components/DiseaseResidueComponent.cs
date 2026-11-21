namespace Content.Shared._Sunrise.Disease.Components;

/// <summary>
/// Вещь, на которой оседает зараза от касаний.
/// </summary>
/// <remarks>
/// Обычно это перчатки, но подойдёт что угодно, чем трогают: фартук, халат, тряпка или инструмент
/// в руках. Сама вещь может защищать владельца через <see cref="DiseaseProtectionComponent"/>,
/// но собранную заразу несёт дальше: владелец передаёт её каждому, кого трогает, а снявший вещь
/// голыми руками рискует сам. Нитрил и лабораторные перчатки отталкивают заразу,
/// поэтому у них <see cref="PickUpChance"/> равен нулю.
/// Висит в shared только ради загрузки прототипов на клиенте, логика серверная.
/// </remarks>
[RegisterComponent]
public sealed partial class DiseaseResidueComponent : Component
{
    /// <summary>
    /// Шанс, что при касании заражённого зараза останется на вещи. Ноль — вещь не загрязняется.
    /// </summary>
    [DataField]
    public float PickUpChance = 1f;

    /// <summary>
    /// Шанс заразить того, кого коснулись загрязнённой вещью.
    /// </summary>
    [DataField]
    public float ContactChance = 0.15f;
}
