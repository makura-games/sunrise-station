using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

// Не менять
namespace Content.Shared.Ghost;

public abstract partial class SharedGhostSystem
{

    /// <summary>
    /// Хранилище данных о варпе-игроке
    /// </summary>
    /// <param name="Entity"><see cref="NetEntity"/> игрока</param>
    /// <param name="Name">Имя игрока</param>
    /// <param name="JobId">ID работы игрока</param>
    /// <param name="DepartmentId">ID департамента работы игрока</param>
    /// <param name="IsGhost">Является ли сущность призраком?</param>
    /// <param name="IsLeft">Вышел ли игрок из тела этой сущности?</param>
    /// <param name="IsDead">Мертва ли сущность?</param>
    [Serializable, NetSerializable]
    public record struct GhostWarpPlayer(
        NetEntity Entity,
        string Name,
        ProtoId<JobPrototype>? JobId,
        ProtoId<DepartmentPrototype> DepartmentId,
        bool IsGhost,
        bool IsLeft,
        bool IsDead) : INamedGhostWarp
    {
        public readonly NetEntity Entity = Entity;
        public string Name { get; } = Name;

        public readonly ProtoId<JobPrototype>? JobId = JobId;

        public readonly ProtoId<DepartmentPrototype> DepartmentId = DepartmentId;

        public readonly bool IsGhost = IsGhost;

        public readonly bool IsLeft = IsLeft;

        public readonly bool IsDead = IsDead;
    }

    /// <summary>
    /// Хранилище данных о варпе-игроке с ролью антагониста. Они отображаются отдельно от всех
    /// </summary>
    /// <param name="Entity"><see cref="NetEntity"/> сущности игрока</param>
    /// <param name="Name">Имя игрока</param>
    /// <param name="AntagonistName">Название антагониста</param>
    /// <param name="AntagonistDescription">Описание антагониста</param>
    /// <param name="Priority">Приоритет отображения антагониста</param>
    [Serializable, NetSerializable]
    public record struct GhostWarpGlobalAntagonist(
        NetEntity Entity,
        string Name,
        string AntagonistName,
        string AntagonistDescription,
        int Priority) : INamedGhostWarp
    {
        public readonly NetEntity Entity = Entity;

        public string Name { get; } = Name;

        public readonly string AntagonistName = AntagonistName;

        public readonly string AntagonistDescription = AntagonistDescription;

        public readonly int Priority = Priority;

    }

    /// <summary>
    /// Хранилище данных о варпе-локации
    /// </summary>
    /// <param name="Entity"><see cref="NetEntity"/> сущности точки телепорта</param>
    /// <param name="Name">Название локации</param>
    /// <param name="Description">Описание локации</param>
    [Serializable, NetSerializable]
    public record struct GhostWarpPlace(NetEntity Entity, string Name, string Description) : INamedGhostWarp
    {
        public readonly NetEntity Entity = Entity;

        public string Name { get; } = Name;

        public readonly string Description = Description;

    }

    /// <summary>
    /// Ивент, передающий информацию о варпах для их актуализации.
    /// </summary>
    /// <param name="players">Варпы-игроки <see cref="GhostWarpPlayer"/></param>
    /// <param name="places">Варпы-локации <see cref="GhostWarpPlace"/></param>
    /// <param name="antagonists">Варпы-игроки с ролью антагониста <see cref="GhostWarpGlobalAntagonist"/></param>
    [Serializable, NetSerializable]
    public sealed class GhostWarpsResponseEvent(
        List<GhostWarpPlayer> players,
        List<GhostWarpPlace> places,
        List<GhostWarpGlobalAntagonist> antagonists) : EntityEventArgs
    {
        public readonly List<GhostWarpPlayer> Players = players;

        public readonly List<GhostWarpPlace> Places = places;

        public readonly List<GhostWarpGlobalAntagonist> Antagonists = antagonists;
    }

    /// <summary>
    /// Интерфейс, который говорит, что у этого варпа есть имя.
    /// Нужен, чтобы реализовать сортировки по имени в генерации кнопок с помощью одной функции для всех 3 типов варпов
    /// </summary>
    public interface INamedGhostWarp
    {
        public string Name { get; }
    }
}
