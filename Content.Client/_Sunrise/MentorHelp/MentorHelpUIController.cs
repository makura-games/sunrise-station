using Content.Client._Sunrise.Lobby.UI;
using Content.Client.Administration.Managers;
using Content.Client.Gameplay;
using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using Content.Shared._Sunrise.MentorHelp;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.Administration;
using Content.Shared.Input;
using JetBrains.Annotations;
using Robust.Client.Audio;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Input.Binding;
using Robust.Shared.Localization;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Audio;

namespace Content.Client._Sunrise.MentorHelp;

/// <summary>
/// Контроллер интерфейса системы менторской помощи.
/// </summary>
[UsedImplicitly]
public sealed partial class MentorHelpUIController : UIController, IOnSystemChanged<MentorHelpSystem>, IOnStateChanged<GameplayState>, IOnStateChanged<LobbyState>
{
    [Dependency] private IClientAdminManager _adminManager = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private ILocalizationManager _loc = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IClyde _clyde = default!;
    [UISystemDependency] private readonly AudioSystem _audio = default!;

    private MentorHelpSystem? _mentorHelpSystem;
    public IMentorHelpUIHandler? UIHelper;
    private readonly HashSet<int> _unreadTicketIds = new();
    private readonly Dictionary<int, int> _lastMessageIdByTicket = new();

    // Последнее состояние тикетов. Нужны для фильтрации "моих" тикетов и авто-открытия.
    private readonly Dictionary<int, MentorHelpTicketData> _ticketDataById = new();
    private bool _mentorHelpSoundEnabled;
    private static readonly SoundSpecifier? MentorHelpSound =
        new SoundPathSpecifier("/Audio/_Sunrise/Effects/adminticketopen.ogg", AudioParams.Default.WithVolume(-3f));

    private Button? LobbyMHelpButton => (UIManager.ActiveScreen as SunriseLobbyGui)?.MHelpButton;
    private MenuButton? GameMHelpButton => UIManager.GetActiveUIWidgetOrNull<GameTopMenuBar>()?.MHelpButton;

    protected override string SawmillName => "c.s.go.es.mhelp";

    public override void Initialize()
    {
        base.Initialize();

        _adminManager.AdminStatusUpdated += OnAdminStatusUpdated;
        _config.OnValueChanged(SunriseCCVars.MentorHelpSoundEnabled, v => _mentorHelpSoundEnabled = v, true);
    }

    public void OnSystemLoaded(MentorHelpSystem system)
    {
        _mentorHelpSystem = system;
        _mentorHelpSystem.OnTicketUpdated += OnTicketUpdated;
        _mentorHelpSystem.OnTicketsListReceived += OnTicketsListReceived;
        _mentorHelpSystem.OnTicketMessagesReceived += OnTicketMessagesReceived;
        _mentorHelpSystem.OnOpenTicketReceived += OnOpenTicketReceived;
        _mentorHelpSystem.OnPlayerTypingUpdated += OnPlayerTypingUpdated;

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.OpenMentorHelp,
                InputCmdHandler.FromDelegate(_ => Open()))
            .Register<MentorHelpUIController>();
    }

    private void OnOpenTicketReceived(object? sender, MentorHelpOpenTicketMessage message)
    {
        EnsureUIHelper();

        if (UIHelper == null)
            return;

        if (!UIHelper.IsOpen)
        {
            UIHelper.OpenWindow();
            UIHelper.OpenTicket(message.TicketId);
            return;
        }

        if (UIHelper.CurrentTicketId == message.TicketId)
        {
            _mentorHelpSystem?.RequestTicketMessages(message.TicketId);
            return;
        }

        UIHelper.OpenTicket(message.TicketId);
    }

    public void OnSystemUnloaded(MentorHelpSystem system)
    {
        CommandBinds.Unregister<MentorHelpUIController>();

        if (_mentorHelpSystem != null)
        {
            _mentorHelpSystem.OnTicketUpdated -= OnTicketUpdated;
            _mentorHelpSystem.OnTicketsListReceived -= OnTicketsListReceived;
            _mentorHelpSystem.OnTicketMessagesReceived -= OnTicketMessagesReceived;
            _mentorHelpSystem.OnOpenTicketReceived -= OnOpenTicketReceived;
            _mentorHelpSystem.OnPlayerTypingUpdated -= OnPlayerTypingUpdated;
            _mentorHelpSystem = null;
        }

        if (GameMHelpButton != null)
            GameMHelpButton.OnPressed -= MHelpButtonPressed;

        if (LobbyMHelpButton != null)
            LobbyMHelpButton.OnPressed -= MHelpButtonPressed;
    }

    public void OnStateEntered(GameplayState state)
    {
        if (GameMHelpButton != null)
        {
            // Защита от повторной подписки, OnStateEntered может вызываться несколько раз
            // Аналогично в Content.Client/UserInterface/Systems/Bwoink/AHelpUIController.cs (метод OnStateEntered)
            GameMHelpButton.OnPressed -= MHelpButtonPressed;
            GameMHelpButton.OnPressed += MHelpButtonPressed;
            GameMHelpButton.Pressed = UIHelper?.IsOpen ?? false;
            UpdateButtonStyling();
        }
    }

    public void OnStateExited(GameplayState state)
    {
        if (GameMHelpButton != null)
            GameMHelpButton.OnPressed -= MHelpButtonPressed;
    }

    public void OnStateEntered(LobbyState state)
    {
        if (LobbyMHelpButton != null)
        {
            // То же самое для лобби, см. Content.Client/UserInterface/Systems/Bwoink/AHelpUIController.cs (метод OnStateEntered)
            LobbyMHelpButton.OnPressed -= MHelpButtonPressed;
            LobbyMHelpButton.OnPressed += MHelpButtonPressed;
            LobbyMHelpButton.Pressed = UIHelper?.IsOpen ?? false;
            UpdateButtonStyling();
        }
    }

    public void OnStateExited(LobbyState state)
    {
        if (LobbyMHelpButton != null)
            LobbyMHelpButton.OnPressed -= MHelpButtonPressed;
    }

    private void MHelpButtonPressed(BaseButton.ButtonEventArgs obj)
    {
        Open();
    }

    private void OnAdminStatusUpdated()
    {
        if (UIHelper == null || !UIHelper.IsOpen)
            return;

        EnsureUIHelper();
    }

    private void OnTicketUpdated(object? sender, MentorHelpTicketUpdateMessage message)
    {
        // Новый тикет
        var isNewTicket = !_ticketDataById.ContainsKey(message.Ticket.Id);
        _ticketDataById[message.Ticket.Id] = message.Ticket;

        EnsureUIHelper();

        if (UIHelper == null)
            return;

        // Звук для менторов только при появлении нового тикета
        if (isNewTicket && _mentorHelpSoundEnabled && _adminManager.HasFlag(AdminFlags.Mentor) && IsRelevantTicket(message.Ticket.Id))
        {
            _audio.PlayGlobal(MentorHelpSound, Filter.Local(), false);

            if (!UIHelper.IsOpen)
                _clyde.RequestWindowAttention();
        }

        if (!IsRelevantTicket(message.Ticket.Id))
            _unreadTicketIds.Remove(message.Ticket.Id);

        UpdateButtonStyling();

        UIHelper.TicketUpdated(message.Ticket);

        if (UIHelper.IsOpen && UIHelper.CurrentTicketId == message.Ticket.Id)
            _mentorHelpSystem?.RequestTicketMessages(message.Ticket.Id);

    }

    private void OnTicketsListReceived(object? sender, MentorHelpTicketsListMessage message)
    {
        EnsureUIHelper();
        foreach (var ticket in message.Tickets)
        {
            _ticketDataById[ticket.Id] = ticket;

            if (!IsRelevantTicket(ticket.Id))
                _unreadTicketIds.Remove(ticket.Id);
        }
        UpdateButtonStyling();

        if (UIHelper == null)
            return;

        UIHelper.TicketsListReceived(message.Tickets);
    }

    private void OnTicketMessagesReceived(object? sender, MentorHelpTicketMessagesMessage message)
    {
        EnsureUIHelper();

        UpdateUnreadTickets(message.TicketId, message.Messages);

        // Звук только на новые входящие сообщения
        TryPlaySoundForNewMessage(message.TicketId, message.Messages);

        var autoOpen = _config.GetCVar(SunriseCCVars.MentorHelpAutoOpenOnNewMessage);
        var shouldAutoOpen = autoOpen && ShouldAutoOpenTicket(message.TicketId, message.Messages);

        // Автооткрытие работает только когда окно закрыто, ибо раздражает постоянными переключениями
        if (shouldAutoOpen && UIHelper != null && !UIHelper.IsOpen)
            UIHelper.OpenTicket(message.TicketId);

        if (UIHelper == null)
            return;

        UIHelper.TicketMessagesReceived(message.TicketId, message.Messages);
    }

    /// <summary>
    /// Проигрывает звук только на новое входящее сообщение по тикету
    /// </summary>
    private void TryPlaySoundForNewMessage(int ticketId, List<MentorHelpMessageData> messages)
    {
        if (!_mentorHelpSoundEnabled || messages.Count == 0)
            return;

        var lastMessage = messages[^1];

        if (!_lastMessageIdByTicket.TryGetValue(ticketId, out var previousMessageId))
        {
            _lastMessageIdByTicket[ticketId] = lastMessage.Id;
            return;
        }

        // Не новое сообщение
        if (previousMessageId == lastMessage.Id)
            return;

        _lastMessageIdByTicket[ticketId] = lastMessage.Id;

        var localUser = _playerManager.LocalUser;

        // Без звука свое сообщение
        if (localUser == null || lastMessage.SenderUserId == localUser.Value)
            return;

        // Тикет не релевантентен
        if (!IsRelevantTicket(ticketId))
            return;

        var isViewingTicket = UIHelper != null && UIHelper.IsOpen && UIHelper.CurrentTicketId == ticketId;
        var isWindowClosed = UIHelper != null && !UIHelper.IsOpen;

        // Окно открыто, но мы не в этом тикете
        if (!isViewingTicket && !isWindowClosed)
            return;

        // Новое входящее сообщение
        _audio.PlayGlobal(MentorHelpSound, Filter.Local(), false);

        if (isWindowClosed)
            _clyde.RequestWindowAttention();
    }

    private void OnPlayerTypingUpdated(object? sender, MentorHelpPlayerTypingUpdated message)
    {
        UIHelper?.PlayerTypingUpdated(message.TicketId, message.PlayerName, message.Typing);
    }

    public void EnsureUIHelper()
    {
        var hasMentorPerms = _adminManager.HasFlag(AdminFlags.Mentor);

        if (UIHelper != null && UIHelper.HasMentorPermissions == hasMentorPerms)
            return;

        UIHelper?.Dispose();
        if (!TryGetLocalUserId(out var ownerUserId))
            return;

        UIHelper = CreateUiHandler(ownerUserId, hasMentorPerms);

        UIHelper.OnClose += () => { SetMentorHelpPressed(false); };
    }

    /// <summary>
    /// Открыть окно менторской помощи.
    /// </summary>
    public void Open()
    {
        if (!TryGetLocalUserId(out _))
            return;

        EnsureUIHelper();
        if (UIHelper == null)
            return;

        UIHelper.OpenWindow();

        SetMentorHelpPressed(true);
    }

    private IMentorHelpUIHandler CreateUiHandler(NetUserId ownerUserId, bool hasMentorPerms)
    {
        return hasMentorPerms
            ? new MentorMentorHelpUIHandler(ownerUserId, _mentorHelpSystem)
            : new PlayerMentorHelpUIHandler(ownerUserId, _mentorHelpSystem);
    }

    private bool TryGetLocalUserId(out NetUserId userId)
    {
        if (_playerManager.LocalUser is not { } localUser)
        {
            userId = default;
            return false;
        }

        userId = localUser;
        return true;
    }

    /// <summary>
    /// Закрыть окно менторской помощи.
    /// </summary>
    public void Close()
    {
        UIHelper?.CloseWindow();
        SetMentorHelpPressed(false);
    }

    private void SetMentorHelpPressed(bool pressed)
    {
        UIManager.ClickSound();
        UnreadTicketsRead();

        if (GameMHelpButton != null)
            GameMHelpButton.Pressed = pressed;

        if (LobbyMHelpButton != null)
            LobbyMHelpButton.Pressed = pressed;
    }

    private void UnreadTicketsRead()
    {
        _unreadTicketIds.Clear();
        UpdateButtonStyling();
    }

    private void UpdateUnreadTickets(int ticketId, List<MentorHelpMessageData> messages)
    {
        var localUser = _playerManager.LocalUser;
        if (localUser == null)
            return;

        if (!IsRelevantTicket(ticketId))
        {
            _unreadTicketIds.Remove(ticketId);
            UpdateButtonStyling();
            return;
        }

        if (messages.Count == 0)
        {
            _unreadTicketIds.Remove(ticketId);
            UpdateButtonStyling();
            return;
        }

        var lastMessage = messages[^1];
        var isIncoming = lastMessage.SenderUserId != localUser.Value;
        var isViewingTicket = UIHelper != null && UIHelper.IsOpen && UIHelper.CurrentTicketId == ticketId;
        var unread = isIncoming && !isViewingTicket;

        if (unread)
            _unreadTicketIds.Add(ticketId);

        else
            _unreadTicketIds.Remove(ticketId);

        UpdateButtonStyling();
    }

    private bool IsRelevantTicket(int ticketId)
    {
        var localUser = _playerManager.LocalUser;
        if (localUser == null || !_ticketDataById.TryGetValue(ticketId, out var ticket))
            return false;

        var localId = localUser.Value;
        var isMentor = _adminManager.HasFlag(AdminFlags.Mentor);

        if (isMentor)
            return ticket.AssignedToUserId == null || ticket.AssignedToUserId == localId;

        return ticket.PlayerId == localId;
    }

    private bool ShouldAutoOpenTicket(int ticketId, List<MentorHelpMessageData> messages)
    {
        var localUser = _playerManager.LocalUser;
        if (localUser == null || !_ticketDataById.TryGetValue(ticketId, out var ticket))
            return false;

        var localId = localUser.Value;
        var isOwner = ticket.PlayerId == localId;
        var isAssignedToMe = ticket.AssignedToUserId == localId;

        // Авто-открытие только у автора тикета и назначенного ментора
        if (!isOwner && !isAssignedToMe)
            return false;

        if (messages.Count == 0)
            return false;

        var lastMessage = messages[^1];
        return lastMessage.SenderUserId != localId;
    }

    private void UpdateButtonStyling()
    {
        var unreadCount = _unreadTicketIds.Count;
        var hasUnread = unreadCount > 0;
        var displayCount = unreadCount;

        if (hasUnread)
        {
            GameMHelpButton?.StyleClasses.Add("StyleClassRedTopButton");
            LobbyMHelpButton?.StyleClasses.Add("ButtonColorRed");
        }
        else
        {
            GameMHelpButton?.StyleClasses.Remove("StyleClassRedTopButton");
            LobbyMHelpButton?.StyleClasses.Remove("ButtonColorRed");
        }

        if (LobbyMHelpButton != null)
        {
            var baseText = _loc.GetString("ui-lobby-mhelp-button");
            LobbyMHelpButton.Text = displayCount > 0 ? $"{baseText} ({displayCount})" : baseText;
        }

        if (GameMHelpButton != null)
        {
            var baseTooltip = _loc.GetString("ui-options-function-open-mentor-help");
            GameMHelpButton.ToolTip = displayCount > 0 ? $"{baseTooltip} ({displayCount})" : baseTooltip;
        }
    }
}

/// <summary>
/// Интерфейс обработчиков окна менторской помощи.
/// </summary>
public interface IMentorHelpUIHandler : IDisposable
{
    bool IsOpen { get; }
    bool HasMentorPermissions { get; }
    int? CurrentTicketId { get; }
    event Action? OnClose;

    void OpenWindow();
    void CloseWindow();
    void OpenTicket(int ticketId);
    void TicketUpdated(MentorHelpTicketData ticket);
    void TicketsListReceived(List<MentorHelpTicketData> tickets);
    void TicketMessagesReceived(int ticketId, List<MentorHelpMessageData> messages);
    void PlayerTypingUpdated(int ticketId, string playerName, bool typing);
}

public sealed class PlayerMentorHelpUIHandler : IMentorHelpUIHandler
{
    public bool IsOpen { get; private set; }
    public bool HasMentorPermissions => false;
    public event Action? OnClose;
    public int? CurrentTicketId { get; private set; }

    private readonly NetUserId _ownerUserId;
    private readonly MentorHelpSystem? _mentorHelpSystem;
    private MentorHelpWindow? _window;

    public PlayerMentorHelpUIHandler(NetUserId ownerUserId, MentorHelpSystem? mentorHelpSystem)
    {
        _ownerUserId = ownerUserId;
        _mentorHelpSystem = mentorHelpSystem;
    }

    public void OpenWindow()
    {
        if (_window != null)
        {
            _window.MoveToFront();
            IsOpen = true;
            return;
        }

        _window = new MentorHelpWindow();
        _window.MentorHelp.Initialize(_mentorHelpSystem, _ownerUserId, false);
        _window.OnClose += () =>
        {
            _window?.MentorHelp.Cleanup();
            IsOpen = false;
            CurrentTicketId = null;
            OnClose?.Invoke();
            _window = null;
        };

        _window.OpenCentered();
        IsOpen = true;

        _mentorHelpSystem?.RequestTickets(onlyMine: true);
    }

    public void OpenTicket(int ticketId)
    {
        CurrentTicketId = ticketId;
        // Убеждаемся, что окно открыто.
        OpenWindow();
        // Просим контрол сфокусировать тикет, если это возможно.
        _window?.MentorHelp.TryOpenTicket(ticketId);
        // Также запрашиваем сообщения с сервера на случай, если они еще не загружены.
        _mentorHelpSystem?.RequestTicketMessages(ticketId);
    }

    public void CloseWindow()
    {
        CurrentTicketId = null;
        if (_window == null)
        {
            IsOpen = false;
            return;
        }

        _window.Close();
    }

    public void TicketUpdated(MentorHelpTicketData ticket)
    {
        _window?.MentorHelp.UpdateTicket(ticket);
    }

    public void TicketsListReceived(List<MentorHelpTicketData> tickets)
    {
        _window?.MentorHelp.UpdateTicketsList(tickets);
    }

    public void TicketMessagesReceived(int ticketId, List<MentorHelpMessageData> messages)
    {
        _window?.MentorHelp.UpdateTicketMessages(ticketId, messages);
    }

    // Обновление статуса печати, все еще печатает ли игрок
    public void PlayerTypingUpdated(int ticketId, string playerName, bool typing)
    {
        _window?.MentorHelp.UpdatePlayerTyping(ticketId, playerName, typing);
    }

    public void Dispose()
    {
        CloseWindow();
    }
}

/// <summary>
/// Обработчик окна для менторов/админов, которые видят все тикеты и могут управлять ими.
/// </summary>
public sealed class MentorMentorHelpUIHandler : IMentorHelpUIHandler
{
    public int? CurrentTicketId { get; private set; }
    public bool IsOpen { get; private set; }
    public bool HasMentorPermissions => true;
    public event Action? OnClose;

    private readonly NetUserId _ownerUserId;
    private readonly MentorHelpSystem? _mentorHelpSystem;
    private MentorHelpWindow? _window;

    public MentorMentorHelpUIHandler(NetUserId ownerUserId, MentorHelpSystem? mentorHelpSystem)
    {
        _ownerUserId = ownerUserId;
        _mentorHelpSystem = mentorHelpSystem;
    }

    public void OpenWindow()
    {
        if (_window != null)
        {
            _window.MoveToFront();
            IsOpen = true;
            return;
        }

        _window = new MentorHelpWindow();
        _window.MentorHelp.Initialize(_mentorHelpSystem, _ownerUserId, true);
        _window.OnClose += () =>
        {
            _window?.MentorHelp.Cleanup();
            IsOpen = false;
            CurrentTicketId = null;
            OnClose?.Invoke();
            _window = null;
        };

        _window.OpenCentered();
        IsOpen = true;

        _mentorHelpSystem?.RequestTickets(onlyMine: false);
    }

    public void OpenTicket(int ticketId)
    {
        CurrentTicketId = ticketId;
        OpenWindow();
        _window?.MentorHelp.TryOpenTicket(ticketId);
        _mentorHelpSystem?.RequestTicketMessages(ticketId);
    }

    public void CloseWindow()
    {
        CurrentTicketId = null;
        if (_window == null)
        {
            IsOpen = false;
            return;
        }

        _window.Close();
    }

    public void TicketUpdated(MentorHelpTicketData ticket)
    {
        _window?.MentorHelp.UpdateTicket(ticket);
    }

    public void TicketsListReceived(List<MentorHelpTicketData> tickets)
    {
        _window?.MentorHelp.UpdateTicketsList(tickets);
    }

    public void TicketMessagesReceived(int ticketId, List<MentorHelpMessageData> messages)
    {
        _window?.MentorHelp.UpdateTicketMessages(ticketId, messages);
    }

    // Обновление статуса печати, все еще печатает ли ментор
    public void PlayerTypingUpdated(int ticketId, string playerName, bool typing)
    {
        _window?.MentorHelp.UpdatePlayerTyping(ticketId, playerName, typing);
    }

    public void Dispose()
    {
        CloseWindow();
    }
}
