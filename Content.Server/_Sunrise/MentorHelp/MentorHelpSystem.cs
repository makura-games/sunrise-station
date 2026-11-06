using System.Linq;
using System.Threading.Tasks;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.Players.RateLimiting;
using Content.Shared._Sunrise.MentorHelp;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Players.RateLimiting;
using Content.Sunrise.Interfaces.Shared;
using JetBrains.Annotations;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Sunrise.MentorHelp
{
    /// <summary>
    /// Серверная система менторской помощи для управления тикетами.
    /// </summary>
    [UsedImplicitly]
    public sealed partial class MentorHelpSystem : SharedMentorHelpSystem
    {
        private const string RateLimitKey = "MentorHelp";

        [Dependency] private IPlayerManager _playerManager = default!;
        [Dependency] private IAdminManager _adminManager = default!;
        [Dependency] private IConfigurationManager _config = default!;
        [Dependency] private GameTicker _gameTicker = default!;
        [Dependency] private IServerDbManager _dbManager = default!;
        [Dependency] private PlayerRateLimitManager _rateLimit = default!;
        private ISharedSponsorsManager? _sponsorsManager; // Менеджер спонсоров Sunrise.

        private sealed class MentorStatisticsCache
        {
            public List<MentorHelpStatisticsData> WeekStatistics { get; init; } = new();
            public List<MentorHelpStatisticsData> MonthStatistics { get; init; } = new();
            public List<MentorHelpStatisticsData> AllTimeStatistics { get; init; } = new();
        }

        private MentorStatisticsCache? _mentorStatsCache;
        private DateTimeOffset? _mentorStatsCacheTime;
        private uint _mentorStatsCacheVersion;
        private readonly float _mentorCacheInterval = 10;

        public override void Initialize()
        {
            base.Initialize();

            _rateLimit.Register(
                RateLimitKey,
                new RateLimitRegistration(SunriseCCVars.MentorHelpRateLimitPeriod, // Переиспользуем конфиг rate limit от ahelp.
                    SunriseCCVars.MentorHelpRateLimitCount,
                    PlayerRateLimitedAction)
            );

            SubscribeNetworkEvent<MentorHelpClientTypingUpdated>(OnClientTypingUpdated);

            _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
            IoCManager.Instance!.TryResolveType(out _sponsorsManager); // Опциональный менеджер спонсоров Sunrise.
        }

        private void PlayerRateLimitedAction(ICommonSession session)
        {
            Log.Warning($"Player {session.Name} ({session.UserId}) was rate limited for mentor help");
        }

        private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
        {
            // Можно уведомлять менторов о статусе подключения игрока для активных тикетов.
            // Пока оставляем простую реализацию.
        }

        protected override async void OnCreateTicketMessage(MentorHelpCreateTicketMessage message, EntitySessionEventArgs eventArgs)
        {
            var session = eventArgs.SenderSession;

            // Ограничение частоты.
            if (_rateLimit.CountAction(session, RateLimitKey) != RateLimitStatus.Allowed)
                return;

            // Проверяем ввод.
            if (string.IsNullOrWhiteSpace(message.Subject) || string.IsNullOrWhiteSpace(message.Message))
            {
                Log.Warning($"Player {session.Name} ({session.UserId}) tried to create mentor help ticket with empty subject or message");
                return;
            }

            if (message.Subject.Length > 256 || message.Message.Length > 4096)
            {
                Log.Warning($"Player {session.Name} ({session.UserId}) tried to create mentor help ticket with too long subject or message");
                return;
            }

            try
            {
                var now = DateTimeOffset.UtcNow;
                var ticket = new MentorHelpTicket
                {
                    PlayerId = session.UserId,
                    Subject = message.Subject.Trim(),
                    Status = MentorHelpTicketStatus.Open,
                    CreatedAt = now,
                    UpdatedAt = now,
                    RoundId = _gameTicker.RoundId,
                    ServerId = await GetServerIdAsync()
                };

                await _dbManager.AddMentorHelpTicketAsync(ticket);

                var ticketMessage = new MentorHelpMessage
                {
                    TicketId = ticket.Id,
                    SenderUserId = session.UserId.UserId,
                    Message = message.Message.Trim(),
                    SentAt = now,
                    IsStaffOnly = false
                };
                await _dbManager.AddMentorHelpMessageAsync(ticketMessage);

                Log.Info($"Player {session.Name} ({session.UserId}) created mentor help ticket #{ticket.Id}: {ticket.Subject}");

                var ticketData = await ConvertToTicketDataAsync(ticket);
                await NotifyTicketUpdate(ticketData);
                // Просим клиент игрока открыть только что созданный тикет.
                RaiseNetworkEvent(new MentorHelpOpenTicketMessage(ticket.Id), session.Channel);

                var messageData = await ConvertToMessageDataAsync(ticketMessage);
                await NotifyTicketMessage(ticketData, messageData);
            }
            catch (Exception ex)
            {
                Log.Error($"Error creating mentor help ticket for {session.Name} ({session.UserId}): {ex}");
            }
        }

        protected override async void OnClaimTicketMessage(MentorHelpClaimTicketMessage message, EntitySessionEventArgs eventArgs)
        {
            var session = eventArgs.SenderSession;

            // Проверяем права.
            if (!HasMentorPermissions(session))
            {
                Log.Warning($"Player {session.Name} ({session.UserId}) tried to claim mentor help ticket without permissions");
                return;
            }

            try
            {
                var ticket = await _dbManager.GetMentorHelpTicketAsync(message.TicketId);
                if (ticket == null)
                {
                    Log.Warning($"Mentor {session.Name} ({session.UserId}) tried to claim non-existent ticket #{message.TicketId}");
                    return;
                }

                if (ticket.Status == MentorHelpTicketStatus.Closed)
                {
                    Log.Warning($"Mentor {session.Name} ({session.UserId}) tried to claim closed ticket #{message.TicketId}");
                    return;
                }


                if (ticket.AssignedToUserId.HasValue && ticket.AssignedToUserId.Value != session.UserId.UserId)
                    return;


                // Берем тикет в работу.
                ticket.AssignedToUserId = session.UserId.UserId;
                ticket.Status = MentorHelpTicketStatus.Assigned;
                ticket.UpdatedAt = DateTimeOffset.UtcNow;

                await _dbManager.UpdateMentorHelpTicketAsync(ticket);
                InvalidateStatisticsCache();

                Log.Info($"Mentor {session.Name} ({session.UserId}) claimed ticket #{ticket.Id}");

                // Уведомляем все заинтересованные стороны.
                var ticketData = await ConvertToTicketDataAsync(ticket);
                await NotifyTicketUpdate(ticketData);
            }
            catch (Exception ex)
            {
                Log.Error($"Error claiming mentor help ticket #{message.TicketId} by {session.Name} ({session.UserId}): {ex}");
            }
        }

        protected override async void OnReplyMessage(MentorHelpReplyMessage message, EntitySessionEventArgs eventArgs)
        {
            var session = eventArgs.SenderSession;

            // Ограничение частоты.
            if (_rateLimit.CountAction(session, RateLimitKey) != RateLimitStatus.Allowed)
                return;

            try
            {
                var ticket = await _dbManager.GetMentorHelpTicketAsync(message.TicketId);
                if (ticket == null)
                {
                    Log.Warning($"Player {session.Name} ({session.UserId}) tried to reply to non-existent ticket #{message.TicketId}");
                    return;
                }

                if (ticket.Status == MentorHelpTicketStatus.Closed)
                {
                    Log.Warning($"Player {session.Name} ({session.UserId}) tried to reply to closed ticket #{message.TicketId}");
                    return;
                }

                // Проверяем права: игрок может отвечать в свой тикет, менторы/админы — в любой.
                var isTicketOwner = ticket.PlayerId == session.UserId.UserId;
                var hasMentorPerms = HasMentorPermissions(session);

                if (!isTicketOwner && !hasMentorPerms)
                {
                    Log.Warning($"Player {session.Name} ({session.UserId}) tried to reply to ticket #{message.TicketId} without permissions");
                    return;
                }

                // Сообщения только для персонала могут отправлять только менторы/админы.
                if (message.IsStaffOnly && !hasMentorPerms)
                {
                    Log.Warning($"Player {session.Name} ({session.UserId}) tried to send staff-only message without permissions");
                    return;
                }

                // Проверяем сообщение.
                if (string.IsNullOrWhiteSpace(message.Message) || message.Message.Length > 4096)
                {
                    Log.Warning($"Player {session.Name} ({session.UserId}) tried to send invalid message to ticket #{message.TicketId}");
                    return;
                }

                // Создаем сообщение.
                var ticketMessage = new MentorHelpMessage
                {
                    TicketId = message.TicketId,
                    SenderUserId = session.UserId.UserId,
                    Message = message.Message.Trim(),
                    SentAt = DateTimeOffset.UtcNow,
                    IsStaffOnly = message.IsStaffOnly
                };

                await _dbManager.AddMentorHelpMessageAsync(ticketMessage);

                // Обновляем статус тикета.
                if (hasMentorPerms && ticket.Status == MentorHelpTicketStatus.Open)
                {
                    // Ментор ответил в открытый тикет, отмечаем его назначенным.
                    ticket.AssignedToUserId = session.UserId.UserId;
                    ticket.Status = MentorHelpTicketStatus.Assigned;
                }
                else if (hasMentorPerms)
                    // Ментор ответил, ждем ответа игрока.
                    ticket.Status = MentorHelpTicketStatus.AwaitingResponse;

                else if (isTicketOwner && ticket.Status == MentorHelpTicketStatus.AwaitingResponse)
                    // Игрок ответил, снова отмечаем тикет назначенным.
                    ticket.Status = MentorHelpTicketStatus.Assigned;


                ticket.UpdatedAt = DateTimeOffset.UtcNow;
                await _dbManager.UpdateMentorHelpTicketAsync(ticket);
                InvalidateStatisticsCache();

                Log.Info($"Player {session.Name} ({session.UserId}) replied to ticket #{message.TicketId}");

                // Уведомляем заинтересованные стороны.
                var ticketData = await ConvertToTicketDataAsync(ticket);
                var messageData = await ConvertToMessageDataAsync(ticketMessage);
                await NotifyTicketUpdate(ticketData);
                await NotifyTicketMessage(ticketData, messageData);

                if (hasMentorPerms)
                {
                    var userId = new NetUserId(ticket.PlayerId);

                    if (_playerManager.TryGetSessionById(userId, out var authorSession))
                        RaiseNetworkEvent(new MentorHelpOpenTicketMessage(ticket.Id), authorSession.Channel);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error adding reply to mentor help ticket #{message.TicketId} by {session.Name} ({session.UserId}): {ex}");
            }
        }

        protected override async void OnCloseTicketMessage(MentorHelpCloseTicketMessage message, EntitySessionEventArgs eventArgs)
        {
            var session = eventArgs.SenderSession;

            try
            {
                var ticket = await _dbManager.GetMentorHelpTicketAsync(message.TicketId);
                if (ticket == null)
                {
                    Log.Warning($"Player {session.Name} ({session.UserId}) tried to close non-existent ticket #{message.TicketId}");
                    return;
                }

                if (ticket.Status == MentorHelpTicketStatus.Closed)
                {
                    Log.Warning($"Player {session.Name} ({session.UserId}) tried to close already closed ticket #{message.TicketId}");
                    return;
                }

                // Проверяем права: игрок может закрывать свой тикет, менторы/админы — любой.
                var isTicketOwner = ticket.PlayerId == session.UserId.UserId;
                var hasMentorPerms = HasMentorPermissions(session);

                if (!isTicketOwner && !hasMentorPerms)
                {
                    Log.Warning($"Player {session.Name} ({session.UserId}) tried to close ticket #{message.TicketId} without permissions");
                    return;
                }

                // Закрываем тикет.
                ticket.Status = MentorHelpTicketStatus.Closed;
                ticket.ClosedAt = DateTimeOffset.UtcNow;
                ticket.ClosedByUserId = session.UserId.UserId;
                ticket.UpdatedAt = DateTimeOffset.UtcNow;

                await _dbManager.UpdateMentorHelpTicketAsync(ticket);
                InvalidateStatisticsCache();

                Log.Info($"Player {session.Name} ({session.UserId}) closed ticket #{ticket.Id}");

                // Уведомляем заинтересованные стороны.
                var ticketData = await ConvertToTicketDataAsync(ticket);
                await NotifyTicketUpdate(ticketData);
            }
            catch (Exception ex)
            {
                Log.Error($"Error closing mentor help ticket #{message.TicketId} by {session.Name} ({session.UserId}): {ex}");
            }
        }

        protected override async void OnRequestTicketsMessage(MentorHelpRequestTicketsMessage message, EntitySessionEventArgs eventArgs)
        {
            var session = eventArgs.SenderSession;

            try
            {
                List<MentorHelpTicket> tickets;

                if (message.OnlyMine)
                    tickets = await _dbManager.GetMentorHelpTicketsByPlayerAsync(session.UserId.UserId);

                else
                {
                    // Ментор/админ запрашивает все тикеты, открытые и закрытые.
                    if (!HasMentorPermissions(session))
                    {
                        Log.Warning($"Player {session.Name} ({session.UserId}) tried to request all mentor help tickets without permissions");
                        return;
                    }

                    // Получаем для менторов и открытые, и закрытые тикеты.
                    var openTickets = await _dbManager.GetOpenMentorHelpTicketsAsync();
                    var closedTickets = await _dbManager.GetClosedMentorHelpTicketsAsync();
                    tickets = openTickets.Concat(closedTickets).ToList();
                }

                var ticketDataList = await GetTicketDataListAsync(tickets);

                RaiseNetworkEvent(new MentorHelpTicketsListMessage(ticketDataList), session.Channel);
            }
            catch (Exception ex)
            {
                Log.Error($"Error requesting mentor help tickets for {session.Name} ({session.UserId}): {ex}");
            }
        }

        protected override async void OnUnassignTicketMessage(MentorHelpUnassignTicketMessage message, EntitySessionEventArgs eventArgs)
        {
            var session = eventArgs.SenderSession;

            if (!HasMentorPermissions(session))
            {
                Log.Warning($"Player {session.Name} ({session.UserId}) tried to unassign mentor help ticket without permissions");
                return;
            }

            try
            {
                var ticket = await _dbManager.GetMentorHelpTicketAsync(message.TicketId);
                if (ticket == null)
                {
                    Log.Warning($"Mentor {session.Name} ({session.UserId}) tried to unassign non-existent ticket #{message.TicketId}");
                    return;
                }

                if (ticket.Status == MentorHelpTicketStatus.Closed)
                {
                    Log.Warning($"Mentor {session.Name} ({session.UserId}) tried to unassign closed ticket #{message.TicketId}");
                    return;
                }

                ticket.AssignedToUserId = null;
                ticket.Status = MentorHelpTicketStatus.Open;
                ticket.UpdatedAt = DateTimeOffset.UtcNow;

                await _dbManager.UpdateMentorHelpTicketAsync(ticket);
                InvalidateStatisticsCache();

                Log.Info($"Mentor {session.Name} ({session.UserId}) unassigned ticket #{ticket.Id}");

                var ticketData = await ConvertToTicketDataAsync(ticket);
                await NotifyTicketUpdate(ticketData);
            }
            catch (Exception ex)
            {
                Log.Error($"Error unassigning mentor help ticket #{message.TicketId} by {session.Name} ({session.UserId}): {ex}");
            }
        }

        private bool HasMentorPermissions(ICommonSession session)
        {
            var adminData = _adminManager.GetAdminData(session);
            return adminData?.HasFlag(AdminFlags.Mentor) ?? false;
        }

        private Task<int?> GetServerIdAsync()
        {
            // Реализация зависит от того, как отслеживается ID сервера.
            // Пока возвращаем null.
            return Task.FromResult<int?>(null);
        }
        private void InvalidateStatisticsCache() // Чистка кеша
        {
            _mentorStatsCacheVersion++;
            _mentorStatsCache = null;
            _mentorStatsCacheTime = null;
        }

        private async Task<MentorStatisticsCache> BuildStatisticsCacheAsync(DateTimeOffset now)
        {
            var weekStatistics = await _dbManager.GetMentorHelpStatisticsAsync(now.AddDays(-7)); // Неделя
            var monthStatistics = await _dbManager.GetMentorHelpStatisticsAsync(now.AddMonths(-1)); // Месяц
            var allTimeStatistics = await _dbManager.GetMentorHelpStatisticsAsync(null); // За все время

            var mentorUserIds = CollectMentorUserIds(weekStatistics, monthStatistics, allTimeStatistics);
            var activeMentorIds = await GetActiveMentorIdsAsync(mentorUserIds);

            var mentorNames = await _dbManager.GetPlayerNamesBatchAsync(activeMentorIds);

            return new MentorStatisticsCache
            {
                WeekStatistics = ConvertStatistics(weekStatistics, mentorNames, activeMentorIds),
                MonthStatistics = ConvertStatistics(monthStatistics, mentorNames, activeMentorIds),
                AllTimeStatistics = ConvertStatistics(allTimeStatistics, mentorNames, activeMentorIds)
            };
        }

        private static bool HasAdminFlag(Admin admin, Dictionary<int, AdminRank> adminRanksById, AdminFlags flag)
        {
            if (admin.Suspended || admin.Deadminned)
                return false;

            var flags = AdminFlags.None;

            if (admin.AdminRankId != null &&
                adminRanksById.TryGetValue(admin.AdminRankId.Value, out var adminRank))
                flags = AdminFlagsHelper.NamesToFlags(adminRank.Flags.Select(rankFlag => rankFlag.Flag));

            foreach (var dbFlag in admin.Flags)
            {
                var adminFlag = AdminFlagsHelper.NameToFlag(dbFlag.Flag);
                if (dbFlag.Negative)
                    flags &= ~adminFlag;
                else
                    flags |= adminFlag;
            }

            return flags.HasFlag(flag);
        }

        private List<MentorHelpStatisticsData> ConvertStatistics(
            List<MentorHelpStatistics> statistics,
            Dictionary<Guid, string> mentorNames,
            HashSet<Guid> activeMentorIds)
        {
            var result = new List<MentorHelpStatisticsData>(statistics.Count);

            foreach (var stat in statistics)
            {
                if (!activeMentorIds.Contains(stat.MentorUserId))
                    continue;

                mentorNames.TryGetValue(stat.MentorUserId, out var mentorName);

                result.Add(new MentorHelpStatisticsData
                {
                    MentorName = mentorName ?? Loc.GetString("mentor-help-unknown-user"),
                    TicketsClosed = stat.TicketsClosed,
                    MessagesCount = stat.MessagesCount
                });
            }

            result.Sort((left, right) =>
            {
                var compare = right.TicketsClosed.CompareTo(left.TicketsClosed);
                if (compare != 0)
                    return compare;

                compare = right.MessagesCount.CompareTo(left.MessagesCount);
                if (compare != 0)
                    return compare;

                return string.Compare(left.MentorName, right.MentorName, StringComparison.Ordinal);
            });

            return result;
        }

        protected override async void OnRequestStatisticsMessage(MentorHelpRequestStatisticsMessage message, EntitySessionEventArgs eventArgs)
        {
            var session = eventArgs.SenderSession;

            if (!HasMentorPermissions(session))
            {
                Log.Warning($"Player {session.Name} ({session.UserId}) tried to request mentor help statistics without permissions");
                return;
            }

            try
            {
                var cacheToSend = await GetStatisticsCacheAsync();

                RaiseNetworkEvent(new MentorHelpStatisticsMessage(
                    cacheToSend.WeekStatistics,
                    cacheToSend.MonthStatistics,
                    cacheToSend.AllTimeStatistics), session.Channel);
            }
            catch (Exception ex)
            {
                Log.Error($"Error requesting mentor help statistics for {session.Name} ({session.UserId}): {ex}");
            }
        }

        protected override async void OnRequestTicketMessagesMessage(MentorHelpRequestTicketMessagesMessage message, EntitySessionEventArgs eventArgs)
        {
            var session = eventArgs.SenderSession;

            Log.Info("Received RequestTicketMessages for ticket #{0} from {1} ({2})", message.TicketId, session.Name, session.UserId);

            try
            {
                var ticket = await _dbManager.GetMentorHelpTicketAsync(message.TicketId);
                if (ticket == null)
                {
                    Log.Warning($"Player {session.Name} ({session.UserId}) tried to request messages for non-existent ticket #{message.TicketId}");
                    return;
                }

                var hasMentorPerms = HasMentorPermissions(session);
                var isTicketOwner = ticket.PlayerId == session.UserId.UserId;

                if (!hasMentorPerms && !isTicketOwner)
                {
                    Log.Warning($"Player {session.Name} ({session.UserId}) tried to request messages for ticket #{message.TicketId} without permissions");
                    return;
                }

                var messageDatas = await GetTicketMessagesDataAsync(message.TicketId, hasMentorPerms);

                RaiseNetworkEvent(new MentorHelpTicketMessagesMessage(message.TicketId, messageDatas), session.Channel);
                Log.Info("Sent {0} messages for ticket #{1} to {2} ({3})", messageDatas.Count, message.TicketId, session.Name, session.UserId);
            }
            catch (Exception ex)
            {
                Log.Error($"Error requesting mentor help messages for ticket #{message.TicketId} by {session.Name} ({session.UserId}): {ex}");
            }
        }


        private MentorHelpTicketData ConvertToTicketData(MentorHelpTicket ticket, Dictionary<Guid, string> playerNames)
        {
            playerNames.TryGetValue(ticket.PlayerId, out var playerName);

            return CreateTicketData(
                ticket,
                playerName ?? Loc.GetString("mentor-help-unknown-user"),
                TryGetPlayerName(playerNames, ticket.AssignedToUserId),
                TryGetPlayerName(playerNames, ticket.ClosedByUserId));
        }

        private async Task<MentorHelpTicketData> ConvertToTicketDataAsync(MentorHelpTicket ticket)
        {
            var playerNameTask = GetPlayerNameAsync(ticket.PlayerId);
            var assignedToNameTask = GetOptionalPlayerNameAsync(ticket.AssignedToUserId);
            var closedByNameTask = GetOptionalPlayerNameAsync(ticket.ClosedByUserId);

            var playerName = await playerNameTask;
            var assignedToName = await assignedToNameTask;
            var closedByName = await closedByNameTask;

            return CreateTicketData(
                ticket,
                playerName,
                assignedToName,
                closedByName);
        }

        private async Task<MentorHelpMessageData> ConvertToMessageDataAsync(MentorHelpMessage message)
        {
            var senderUserId = new NetUserId(message.SenderUserId);
            var (username, senderAdminData) = await ResolveMessageSenderContextAsync(senderUserId);


            return new MentorHelpMessageData
            {
                Id = message.Id,
                TicketId = message.TicketId,
                SenderUserId = senderUserId,
                SenderName = username,
                FormattedSender = FormatMessageSender(username, senderUserId, senderAdminData),
                Message = message.Message,
                SentAt = message.SentAt.DateTime,
                IsStaffOnly = message.IsStaffOnly
            };
        }

        private async Task<string> GetPlayerNameAsync(Guid userId)
        {
            var name = await GetStoredPlayerNameAsync(new NetUserId(userId));
            if (string.IsNullOrWhiteSpace(name))
            {
                Log.Warning($"GetPlayerNameAsync: No name found for userId {userId}, returning localized fallback name.");
                return Loc.GetString("mentor-help-unknown-user");
            }
            return name;
        }

        private async Task NotifyTicketUpdate(MentorHelpTicketData ticketData)
        {
            // Уведомляем игрока.
            if (_playerManager.TryGetSessionById(ticketData.PlayerId, out var playerSession))
            {
                RaiseNetworkEvent(new MentorHelpTicketUpdateMessage(ticketData), playerSession.Channel);
            }

            // Уведомляем менторов.
            var mentors = GetTargetMentors();
            foreach (var mentor in mentors)
            {
                RaiseNetworkEvent(new MentorHelpTicketUpdateMessage(ticketData), mentor);
            }
        }

        private async Task NotifyTicketMessage(MentorHelpTicketData ticketData, MentorHelpMessageData messageData)
        {
            var allMessages = await _dbManager.GetMentorHelpMessagesByTicketAsync(ticketData.Id);
            var messageDatas = new List<MentorHelpMessageData>();
            foreach (var msg in allMessages.OrderBy(m => m.SentAt)) // сортировка теперь точно по объектам
            {
                messageDatas.Add(await ConvertToMessageDataAsync(msg));
            }

            // Уведомляем игрока, если сообщение не staff-only.
            if (!messageData.IsStaffOnly && _playerManager.TryGetSessionById(ticketData.PlayerId, out var playerSession))
                RaiseNetworkEvent(new MentorHelpTicketMessagesMessage(ticketData.Id, GetPlayerVisibleMessages(messageDatas)), playerSession.Channel);

            // Уведомляем менторов.
            var mentors = GetTargetMentors();
            foreach (var mentor in mentors)
            {
                RaiseNetworkEvent(new MentorHelpTicketMessagesMessage(ticketData.Id, messageDatas), mentor);
            }
        }

        private IList<INetChannel> GetTargetMentors()
        {
            return _adminManager.ActiveAdmins
                .Where(p => _adminManager.GetAdminData(p)?.HasFlag(AdminFlags.Mentor) ?? false)
                .Select(p => p.Channel)
                .ToList();
        }

        private async void OnClientTypingUpdated(MentorHelpClientTypingUpdated msg, EntitySessionEventArgs args)
        {
            var session = args.SenderSession;
            var ticket = await _dbManager.GetMentorHelpTicketAsync(msg.TicketId);
            if (ticket == null)
                return;

            var update = new MentorHelpPlayerTypingUpdated(msg.TicketId, session.UserId, session.Name, msg.Typing);
            var recipients = new HashSet<INetChannel>();

            if (_playerManager.TryGetSessionById(new NetUserId(ticket.PlayerId), out var authorSession))
            {
                if (!authorSession.UserId.Equals(session.UserId))
                    recipients.Add(authorSession.Channel);
            }

            if (ticket.AssignedToUserId.HasValue &&
                _playerManager.TryGetSessionById(new NetUserId(ticket.AssignedToUserId.Value), out var mentorSession))
            {
                if (!mentorSession.UserId.Equals(session.UserId))
                    recipients.Add(mentorSession.Channel);
            }

            foreach (var recipient in recipients)
                RaiseNetworkEvent(update, recipient);
        }
    }
}
