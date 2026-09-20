using Robust.Shared.Serialization;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Antags.Vampires.Components;

/// <summary>
/// Отслеживает активные лучевые соединения Кровавой связи способности Данталиона
/// </summary>
[RegisterComponent]
public sealed partial class VampireBloodBondBeamComponent : Component
{
    /// <summary>
    /// Прототип визуального луча Кровавой связи.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId VisualPrototype;

    /// <summary>
    /// Активные лучевые соединения, где эта сущность — источник
    /// </summary>
    [DataField]
    public Dictionary<EntityUid, BloodBondBeamConnection> ActiveBeams = new();
}

/// <summary>
/// Данные соединения луча Кровавой связи
/// </summary>
[DataRecord]
public readonly partial record struct BloodBondBeamConnection(
    EntityUid Source,
    EntityUid Target,
    float MaxRange
);

/// <summary>
    /// Сетевое событие создания/обновления луча Кровавой связи на клиенте
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class VampireBloodBondBeamEvent : EntityEventArgs
    {
        /// <summary>
        /// Сетевая сущность источника луча.
        /// </summary>
        public NetEntity Source { get; }
        /// <summary>
        /// Сетевая сущность цели луча.
        /// </summary>
        public NetEntity Target { get; }
        /// <summary>
        /// Создать (true) или удалить (false) луч.
        /// </summary>
        public bool Create { get; }
        /// <summary>
        /// Прототип визуального луча.
        /// </summary>
        public string VisualPrototype { get; }

        public VampireBloodBondBeamEvent(NetEntity source, NetEntity target, bool create, string visualPrototype)
        {
            Source = source;
            Target = target;
            Create = create;
            VisualPrototype = visualPrototype;
        }
    }
