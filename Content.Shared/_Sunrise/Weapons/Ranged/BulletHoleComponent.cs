using System.Numerics;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Whitelist;
using Robust.Shared.Maths;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Weapons.Ranged;

/// <summary>
/// Хранит серверное состояние следов от пуль на поверхности.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(BulletHoleSystem))]
public sealed partial class BulletHoleComponent : Component
{
    /// <summary>
    /// Данные отдельных следов, созданных на этой цели.
    /// </summary>
    [NonSerialized]
    public List<BulletHoleVisualData> Holes = [];
}

/// <summary>
/// Позволяет снаряду оставлять следы от пуль на подходящих целях.
/// </summary>
[RegisterComponent]
public sealed partial class BulletHoleGeneratorComponent : Component
{
    /// <summary>
    /// Тип урона, который должен быть нанесён для создания следа от пули.
    /// </summary>
    [DataField]
    public ProtoId<DamageTypePrototype> RequiredDamageType = "Piercing";

    /// <summary>
    /// Цели, на которых этот снаряд может оставлять следы от пуль.
    /// </summary>
    [DataField]
    public EntityWhitelist? TargetWhitelist;
}

/// <summary>
/// Данные для отображения следа от пули в локальном пространстве цели.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct BulletHoleVisualData(string State, Vector2 Offset, Angle Rotation);

/// <summary>
/// Сетевые данные всех следов от пуль на одной цели.
/// </summary>
[Serializable, NetSerializable]
public sealed class BulletHoleVisualsData : ICloneable
{
    public readonly List<BulletHoleVisualData> Holes;

    public BulletHoleVisualsData(List<BulletHoleVisualData> holes)
    {
        Holes = new List<BulletHoleVisualData>(holes);
    }

    public object Clone()
    {
        return new BulletHoleVisualsData(Holes);
    }
}

[Serializable, NetSerializable]
public enum BulletHoleVisuals : byte
{
    Holes,
}
