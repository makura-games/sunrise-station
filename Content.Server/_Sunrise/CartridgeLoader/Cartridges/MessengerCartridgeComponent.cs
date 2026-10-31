using Content.Shared._Sunrise.Messenger;

namespace Content.Server._Sunrise.CartridgeLoader.Cartridges;

/// <summary>
/// Компонент картриджа мессенджера для КПК
/// </summary>
[RegisterComponent]
public sealed partial class MessengerCartridgeComponent : Component
{
    /// <summary>
    /// Адрес сервера мессенджера, к которому подключен этот КПК
    /// </summary>
    [ViewVariables]
    public string? ServerAddress;

    /// <summary>
    /// ID текущего пользователя в системе мессенджера
    /// </summary>
    [ViewVariables]
    public string? UserId;

    /// <summary>
    /// Зарегистрирован ли пользователь на сервере
    /// </summary>
    [ViewVariables]
    public bool IsRegistered = false;

    /// <summary>
    /// Время последней попытки регистрации
    /// </summary>
    [ViewVariables]
    public TimeSpan? LastRegistrationAttempt;

    /// <summary>
    /// UID загрузчика картриджей (КПК)
    /// </summary>
    [ViewVariables]
    public EntityUid? LoaderUid;

    /// <summary>
    /// Готов ли открытый UI fragment мессенджера принимать состояние.
    /// </summary>
    [ViewVariables]
    public bool UiReady;

    /// <summary>
    /// Список пользователей
    /// </summary>
    [ViewVariables]
    public List<MessengerUser> Users = new();

    /// <summary>
    /// Список групп
    /// </summary>
    [ViewVariables]
    public List<MessengerGroup> Groups = new();

    /// <summary>
    /// История сообщений по чатам
    /// </summary>
    [ViewVariables]
    public Dictionary<string, List<MessengerMessage>> MessageHistory = new();

    /// <summary>
    /// Сообщения для текущего чата
    /// </summary>
    [ViewVariables]
    public List<MessengerMessage> Messages = new();

    /// <summary>
    /// Время последней проверки статуса сервера
    /// </summary>
    [ViewVariables]
    public TimeSpan? LastStatusCheck;

    /// <summary>
    /// Последний запрошенный chatId для истории сообщений
    /// </summary>
    [ViewVariables]
    public string? LastRequestedChatId;

    /// <summary>
    /// Заглушенные личные чаты (chatId)
    /// </summary>
    [ViewVariables]
    public HashSet<string> MutedPersonalChats = new();

    /// <summary>
    /// Заглушенные групповые чаты (groupId)
    /// </summary>
    [ViewVariables]
    public HashSet<string> MutedGroupChats = new();

    /// <summary>
    /// Время последнего обновления списка пользователей
    /// </summary>
    [ViewVariables]
    public TimeSpan? LastUsersUpdate;

    /// <summary>
    /// Количество непрочитанных сообщений с сервера (chatId -> количество)
    /// </summary>
    [ViewVariables]
    public Dictionary<string, int> ServerUnreadCounts = new();

    /// <summary>
    /// Активные приглашения в группы
    /// </summary>
    [ViewVariables]
    public List<MessengerGroupInvite> ActiveInvites = new();

    /// <summary>
    /// Закрепленные чаты (chatId)
    /// </summary>
    [ViewVariables]
    public HashSet<string> PinnedChats = new();
}
