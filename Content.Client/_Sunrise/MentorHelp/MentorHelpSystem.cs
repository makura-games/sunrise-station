using Content.Shared._Sunrise.MentorHelp;
using JetBrains.Annotations;
using Robust.Shared.Timing;

namespace Content.Client._Sunrise.MentorHelp
{
    /// <summary>
    /// Клиентская система менторской помощи.
    /// </summary>
    [UsedImplicitly]
    public sealed partial class MentorHelpSystem : SharedMentorHelpSystem
    {
        [Dependency] private IGameTiming _gameTiming = default!;

        public event EventHandler<MentorHelpTicketUpdateMessage>? OnTicketUpdated;
        public event EventHandler<MentorHelpTicketsListMessage>? OnTicketsListReceived;
        public event EventHandler<MentorHelpTicketMessagesMessage>? OnTicketMessagesReceived;
        public event EventHandler<MentorHelpStatisticsMessage>? OnStatisticsReceived;
        public event EventHandler<MentorHelpOpenTicketMessage>? OnOpenTicketReceived;
        public event EventHandler<MentorHelpPlayerTypingUpdated>? OnPlayerTypingUpdated;
        private const double TypingUpdateResendIntervalSeconds = 1;
        private (TimeSpan Timestamp, bool Typing) _lastTypingUpdateSent;


        protected override void OnCreateTicketMessage(MentorHelpCreateTicketMessage message, EntitySessionEventArgs eventArgs)
        {
            // Клиент не обрабатывает это напрямую.
        }

        protected override void OnClaimTicketMessage(MentorHelpClaimTicketMessage message, EntitySessionEventArgs eventArgs)
        {
            // Клиент не обрабатывает это напрямую.
        }

        protected override void OnReplyMessage(MentorHelpReplyMessage message, EntitySessionEventArgs eventArgs)
        {
            // Клиент не обрабатывает это напрямую.
        }

        protected override void OnCloseTicketMessage(MentorHelpCloseTicketMessage message, EntitySessionEventArgs eventArgs)
        {
            // Клиент не обрабатывает это напрямую.
        }

        protected override void OnRequestTicketsMessage(MentorHelpRequestTicketsMessage message, EntitySessionEventArgs eventArgs)
        {
            // Клиент не обрабатывает это напрямую.
        }

        protected override void OnUnassignTicketMessage(MentorHelpUnassignTicketMessage message, EntitySessionEventArgs eventArgs)
        {
            // Клиент не обрабатывает это напрямую.
        }

        protected override void OnRequestStatisticsMessage(MentorHelpRequestStatisticsMessage message, EntitySessionEventArgs eventArgs)
        {
            // Клиент не обрабатывает это напрямую.
        }

        public override void Initialize()
        {
            base.Initialize();

            SubscribeNetworkEvent<MentorHelpTicketUpdateMessage>(OnTicketUpdate);
            SubscribeNetworkEvent<MentorHelpTicketsListMessage>(OnTicketsList);
            SubscribeNetworkEvent<MentorHelpTicketMessagesMessage>(OnTicketMessages);
            SubscribeNetworkEvent<MentorHelpStatisticsMessage>(OnStatistics);
            SubscribeNetworkEvent<MentorHelpOpenTicketMessage>(OnOpenTicket);
            SubscribeNetworkEvent<MentorHelpPlayerTypingUpdated>(OnTypingUpdated);
        }

        private void OnOpenTicket(MentorHelpOpenTicketMessage message, EntitySessionEventArgs eventArgs)
        {
            OnOpenTicketReceived?.Invoke(this, message);
        }

        private void OnTypingUpdated(MentorHelpPlayerTypingUpdated message, EntitySessionEventArgs eventArgs)
        {
            OnPlayerTypingUpdated?.Invoke(this, message);
        }

        private void OnTicketUpdate(MentorHelpTicketUpdateMessage message, EntitySessionEventArgs eventArgs)
        {
            OnTicketUpdated?.Invoke(this, message);
        }

        private void OnTicketsList(MentorHelpTicketsListMessage message, EntitySessionEventArgs eventArgs)
        {
            OnTicketsListReceived?.Invoke(this, message);
        }

        private void OnTicketMessages(MentorHelpTicketMessagesMessage message, EntitySessionEventArgs eventArgs)
        {
            OnTicketMessagesReceived?.Invoke(this, message);
        }

        /// <summary>
        /// Обрабатывает полученную статистику менторской помощи.
        /// </summary>
        private void OnStatistics(MentorHelpStatisticsMessage message, EntitySessionEventArgs eventArgs)
        {
            OnStatisticsReceived?.Invoke(this, message);
        }

        public void CreateTicket(string subject, string message)
        {
            RaiseNetworkEvent(new MentorHelpCreateTicketMessage(subject, message));
        }

        /// <summary>
        /// Взять тикет менторской помощи в работу.
        /// </summary>
        public void ClaimTicket(int ticketId)
        {
            RaiseNetworkEvent(new MentorHelpClaimTicketMessage(ticketId));
        }

        /// <summary>
        /// Снять назначение с тикета менторской помощи.
        /// </summary>
        public void UnassignTicket(int ticketId)
        {
            RaiseNetworkEvent(new MentorHelpUnassignTicketMessage(ticketId));
        }

        /// <summary>
        /// Ответить в тикет менторской помощи.
        /// </summary>
        public void ReplyToTicket(int ticketId, string message, bool isStaffOnly = false)
        {
            RaiseNetworkEvent(new MentorHelpReplyMessage(ticketId, message, isStaffOnly));
        }

        /// <summary>
        /// Закрыть тикет менторской помощи.
        /// </summary>
        public void CloseTicket(int ticketId)
        {
            RaiseNetworkEvent(new MentorHelpCloseTicketMessage(ticketId));
        }

        /// <summary>
        /// Запросить тикеты: все для менторов/админов или только свои для игроков.
        /// </summary>
        public void RequestTickets(bool onlyMine = false)
        {
            RaiseNetworkEvent(new MentorHelpRequestTicketsMessage(onlyMine));
        }

        /// <summary>
        /// Запросить сообщения конкретного тикета.
        /// </summary>
        public void RequestTicketMessages(int ticketId)
        {
            // Отправляем на сервер запрос сообщений для указанного тикета.
            RaiseNetworkEvent(new MentorHelpRequestTicketMessagesMessage(ticketId));
        }

        /// <summary>
        /// Запросить статистику менторской помощи.
        /// </summary>
        public void RequestStatistics()
        {
            RaiseNetworkEvent(new MentorHelpRequestStatisticsMessage());
        }

        public void SendInputTextUpdated(int ticketId, bool typing)
        {
            if (_lastTypingUpdateSent.Typing == typing &&
                _lastTypingUpdateSent.Timestamp + TimeSpan.FromSeconds(TypingUpdateResendIntervalSeconds) > _gameTiming.RealTime)
            {
                return;
            }

            _lastTypingUpdateSent = (_gameTiming.RealTime, typing);
            RaiseNetworkEvent(new MentorHelpClientTypingUpdated(ticketId, typing));
        }
    }
}
