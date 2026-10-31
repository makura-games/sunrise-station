using Content.Server.Popups;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Chat.Sanitization;

public sealed partial class ChatSanitizationSystem
{
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly EntProtoId SpamMuteStatusEffect = "StatusEffectMuted";

    private readonly Dictionary<NetUserId, List<MessageHistoryEntry>> _messageHistory = new();

    private bool _antiSpamEnabled;
    private int _counterShort;
    private int _counterLong;
    private float _muteDuration;
    private float _timeShort;
    private float _timeLong;

    private void InitializeAntiSpam(ConfigurationMultiSubscriptionBuilder cvarSubscriptions)
    {
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(RoundRestartHistoryCleanup);

        cvarSubscriptions
            .OnValueChanged(SunriseCCVars.ChatAntiSpamEnabled, val => _antiSpamEnabled = val, true)
            .OnValueChanged(SunriseCCVars.ChatAntiSpamShortRepeatLimit, val => _counterShort = val, true)
            .OnValueChanged(SunriseCCVars.ChatAntiSpamLongRepeatLimit, val => _counterLong = val, true)
            .OnValueChanged(SunriseCCVars.ChatAntiSpamMuteDuration, val => _muteDuration = val, true)
            .OnValueChanged(SunriseCCVars.ChatAntiSpamShortWindow, val => _timeShort = val, true)
            .OnValueChanged(SunriseCCVars.ChatAntiSpamLongWindow, val => _timeLong = val, true);
    }

    private void SpamCheck(Entity<ActorComponent> ent, ref TrySendChatMessageEvent args)
    {
        if (ShouldSkipSpamCheck(args.IcChatType))
            return;

        if (!_player.TryGetSessionByEntity(ent, out var session))
            return;

        var now = (float)_timing.CurTime.TotalSeconds;
        var history = GetMessageHistory(session.UserId);

        CompactHistoryAndCountRepeats(history, args.Message, now, out var repeatsShort, out var repeatsLong);
        history.Add(new MessageHistoryEntry(args.Message, now));

        if (repeatsShort > _counterShort || repeatsLong > _counterLong)
            ApplyMuteForSpam(ent, ref args, history);
    }

    private bool ShouldSkipSpamCheck(InGameICChatType? type)
    {
        if (!_antiSpamEnabled)
            return true;

        if (type == InGameICChatType.Emote)
            return true;

        return false;
    }

    private List<MessageHistoryEntry> GetMessageHistory(NetUserId userId)
    {
        if (_messageHistory.TryGetValue(userId, out var history))
            return history;

        history = [];
        _messageHistory[userId] = history;

        return history;
    }

    private void CompactHistoryAndCountRepeats(
        List<MessageHistoryEntry> history,
        string message,
        float now,
        out int repeatsShort,
        out int repeatsLong)
    {
        repeatsShort = 1;
        repeatsLong = 1;
        var writeIndex = 0;

        for (var readIndex = 0; readIndex < history.Count; readIndex++)
        {
            var entry = history[readIndex];
            var age = now - entry.Time;

            if (age > _timeLong)
                continue;

            history[writeIndex++] = entry;

            if (!entry.Message.Equals(message, StringComparison.Ordinal))
                continue;

            repeatsLong++;
            if (age <= _timeShort)
                repeatsShort++;
        }

        if (writeIndex < history.Count)
            history.RemoveRange(writeIndex, history.Count - writeIndex);
    }

    private void ApplyMuteForSpam(EntityUid uid, ref TrySendChatMessageEvent args, List<MessageHistoryEntry> history)
    {
        history.Clear();
        args.Cancelled = true;

        var message = Loc.GetString("spam-mute-text", ("target", uid));
        _popup.PopupEntity(message, uid, uid, PopupType.Large);

        _statusEffects.TryUpdateStatusEffectDuration(uid, SpamMuteStatusEffect, TimeSpan.FromSeconds(_muteDuration));
    }

    private void RoundRestartHistoryCleanup(RoundRestartCleanupEvent ev)
    {
        _messageHistory.Clear();
    }

    private readonly record struct MessageHistoryEntry(string Message, float Time);
}
