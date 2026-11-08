using Content.Shared.AlertLevel;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Communications
{
    [Virtual]
    public partial class SharedCommunicationsConsoleComponent : Component
    {
    }

    [Serializable, NetSerializable]
    public sealed class CommunicationsConsoleInterfaceState : BoundUserInterfaceState
    {
        public readonly bool CanAnnounce;
        public readonly bool CanBroadcast = true;
        public readonly bool CanCall;
        public readonly TimeSpan? ExpectedCountdownEnd;
        public readonly bool CountdownStarted;
        // Sunrise added start - состояние ретрансляции сообщений
        public readonly bool CanRelay;
        public readonly bool IsRelaying;
        public readonly float RelayCooldownRemaining;
        public readonly float RelayTimeRemaining;
        // Sunrise added end

        public CommunicationsConsoleInterfaceState(
            bool canAnnounce,
            bool canCall,
            TimeSpan? expectedCountdownEnd = null,
            bool canRelay = false,
            bool isRelaying = false,
            float relayCooldownRemaining = 0f,
            float relayTimeRemaining = 0f) // Sunrise-Edit
        {
            CanAnnounce = canAnnounce;
            CanCall = canCall;
            ExpectedCountdownEnd = expectedCountdownEnd;
            CountdownStarted = expectedCountdownEnd != null;
            // Sunrise added start - состояние ретрансляции сообщений
            CanRelay = canRelay;
            IsRelaying = isRelaying;
            RelayCooldownRemaining = relayCooldownRemaining;
            RelayTimeRemaining = relayTimeRemaining;
            // Sunrise added end
        }
    }

    [Serializable, NetSerializable]
    public sealed class CommunicationsConsoleSelectAlertLevelMessage(ProtoId<AlertLevelPrototype> level) : BoundUserInterfaceMessage
    {
        public ProtoId<AlertLevelPrototype> Level = level;
    }

    [Serializable, NetSerializable]
    public sealed class CommunicationsConsoleAnnounceMessage : BoundUserInterfaceMessage
    {
        public readonly string Message;

        public CommunicationsConsoleAnnounceMessage(string message)
        {
            Message = message;
        }
    }

    [Serializable, NetSerializable]
    public sealed class CommunicationsConsoleBroadcastMessage : BoundUserInterfaceMessage
    {
        public readonly string Message;
        public CommunicationsConsoleBroadcastMessage(string message)
        {
            Message = message;
        }
    }

    // Sunrise-Start
    [Serializable, NetSerializable]
    public sealed class CommunicationsConsoleToggleRelayMessage : BoundUserInterfaceMessage
    {
    }
    // Sunrise-End

    [Serializable, NetSerializable]
    public sealed class CommunicationsConsoleCallEmergencyShuttleMessage : BoundUserInterfaceMessage
    {
    }

    [Serializable, NetSerializable]
    public sealed class CommunicationsConsoleRecallEmergencyShuttleMessage : BoundUserInterfaceMessage
    {
    }

    [Serializable, NetSerializable]
    public enum CommunicationsConsoleUiKey
    {
        Key
    }
}
