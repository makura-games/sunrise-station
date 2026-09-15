using Robust.Shared.Serialization;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Antags.Vampires.Components;

/// <summary>
/// Отслеживает активные лучевые соединения вытягивания для Обряда Кровеносца
/// </summary>
[RegisterComponent]
public sealed partial class VampireDrainBeamComponent : Component
{
    /// <summary>
    /// Прототип визуального луча вытягивания.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId VisualPrototype;

    /// <summary>
    /// Активные лучевые соединения, где эта сущность — источник
    /// </summary>
    [DataField]
    public Dictionary<EntityUid, DrainBeamConnection> ActiveBeams = new();
}

/// <summary>
/// Данные лучевого соединения вытягивания
/// </summary>
[DataRecord]
public readonly partial record struct DrainBeamConnection(
    EntityUid Source,
    EntityUid Target,
    float MaxRange
);

/// <summary>
    /// Сетевое событие создания/обновления луча вытягивания на клиенте
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class VampireDrainBeamEvent : EntityEventArgs
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

        public VampireDrainBeamEvent(NetEntity source, NetEntity target, bool create, string visualPrototype)
        {
            Source = source;
            Target = target;
            Create = create;
            VisualPrototype = visualPrototype;
        }
    }
