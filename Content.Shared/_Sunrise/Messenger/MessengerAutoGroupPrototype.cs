using Content.Shared.Radio;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Messenger;

/// <summary>
/// Прототип автоматической группы мессенджера (департаменты, общий чат и т.д.)
/// </summary>
[Prototype]
public sealed partial class MessengerAutoGroupPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Название группы (LocId)
    /// </summary>
    [DataField(required: true)]
    public LocId Name { get; private set; } = default!;

    /// <summary>
    /// Уникальный ID группы (используется для идентификации в системе)
    /// </summary>
    [DataField(required: true)]
    public string GroupId { get; private set; } = default!;

    /// <summary>
    /// Связанный радиоканал (для системных оповещений)
    /// </summary>
    [DataField]
    public ProtoId<RadioChannelPrototype>? RadioChannel { get; private set; } = null;

    /// <summary>
    /// Если true, добавляет всех пользователей автоматически
    /// </summary>
    [DataField]
    public bool AddAllUsers { get; private set; } = false;

    /// <summary>
    /// Список департаментов, пользователи которых будут автоматически добавлены в группу
    /// Если пусто и AddAllUsers = false, группа не будет создана автоматически
    /// </summary>
    [DataField]
    public HashSet<ProtoId<DepartmentPrototype>> Departments { get; private set; } = new();

    /// <summary>
    /// Можно ли добавлять/удалять участников вручную
    /// </summary>
    [DataField]
    public bool AllowManualMemberManagement { get; private set; } = false;
}
