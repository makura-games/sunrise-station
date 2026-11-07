#pragma warning disable IDE0130 // Namespace does not match folder structure
using Content.Server._Sunrise.Chat.Sanitization;
using Content.Shared.Chat;

namespace Content.Server.Chat.Systems;

public sealed partial class ChatSystem
{
    private bool TryProcessSunriseChatMessage(
        EntityUid source,
        ref string message,
        InGameICChatType? icChatType = null,
        InGameOOCChatType? oocChatType = null)
    {
        var trySendEvent = new TrySendChatMessageEvent(message, icChatType, oocChatType);
        RaiseLocalEvent(source, ref trySendEvent);

        if (trySendEvent.Cancelled)
            return false;

        message = trySendEvent.Message;
        return true;
    }
}
