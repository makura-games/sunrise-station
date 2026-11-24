#nullable enable
using Content.Shared.Administration;
using JetBrains.Annotations;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Client.Administration.Systems
{
    [UsedImplicitly]
    public sealed partial class BwoinkSystem : SharedBwoinkSystem
    {
        [Dependency] private IGameTiming _timing = default!;

        public event EventHandler<BwoinkTextMessage>? OnBwoinkTextMessageRecieved;
        public event EventHandler<BwoinkCooldownMessage>? OnBwoinkCooldownReceived;
        private (TimeSpan Timestamp, bool Typing) _lastTypingUpdateSent;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeNetworkEvent<BwoinkCooldownMessage>(OnBwoinkCooldownMessage);
        }

        protected override void OnBwoinkTextMessage(BwoinkTextMessage message, EntitySessionEventArgs eventArgs)
        {
            OnBwoinkTextMessageRecieved?.Invoke(this, message);
        }

        private void OnBwoinkCooldownMessage(BwoinkCooldownMessage message, EntitySessionEventArgs eventArgs)
        {
            OnBwoinkCooldownReceived?.Invoke(this, message);
        }

        public void Send(NetUserId channelId, string text, bool playSound, bool adminOnly)
        {
            // Reuse the channel ID as the 'true sender'.
            // Server will ignore this and if someone makes it not ignore this (which is bad, allows impersonation!!!), that will help.
            RaiseNetworkEvent(new BwoinkTextMessage(channelId, channelId, text, playSound: playSound, adminOnly: adminOnly));
            SendInputTextUpdated(channelId, false);
        }

        public void SendInputTextUpdated(NetUserId channel, bool typing)
        {
            if (_lastTypingUpdateSent.Typing == typing &&
                _lastTypingUpdateSent.Timestamp + TimeSpan.FromSeconds(1) > _timing.RealTime)
            {
                return;
            }

            _lastTypingUpdateSent = (_timing.RealTime, typing);
            RaiseNetworkEvent(new BwoinkClientTypingUpdated(channel, typing));
        }

        // Sunrise-Start
        public void LoadDbMessages(NetUserId userId)
        {
            RaiseNetworkEvent(new BwoinkRequestDbMessages(userId));
        }
        // Sunrise-End
    }
}
