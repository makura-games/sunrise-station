using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.Chat;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Server._Sunrise.Chat.Sanitization;

/// <summary>
/// Система автоматической очистки (санитизации) чата.
/// </summary>
public sealed partial class ChatSanitizationSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private ISharedChatManager _chat = default!;
    [Dependency] private IPlayerManager _player = default!;

    private ConfigurationMultiSubscriptionBuilder? _cVarSubscriptions;
    private bool _chatSanitizationEnabled;
    private bool _aggressive;

    private const int MaxAdminAlertMessageLength = 20;

    public override void Initialize()
    {
        base.Initialize();

        var cvarSubscriptions = _configuration.SubscribeMultiple()
            .OnValueChanged(SunriseCCVars.ChatSanitizationEnabled, val => _chatSanitizationEnabled = val, true)
            .OnValueChanged(SunriseCCVars.ChatSanitizationAggressive, val => _aggressive = val, true);

        InitializeAntiSpam(cvarSubscriptions);
        _cVarSubscriptions = cvarSubscriptions;

        SubscribeLocalEvent<ActorComponent, TrySendChatMessageEvent>(OnTrySendChatMessage);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _cVarSubscriptions?.Dispose();
        _cVarSubscriptions = null;
    }

    [GeneratedRegex(@"(?<!\d)\b[-a-z0-9@:%._+~#=]{1,256}\.[a-z]{2,63}\b(?!\d)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlRegex();

    private void OnTrySendChatMessage(Entity<ActorComponent> ent, ref TrySendChatMessageEvent args)
    {
        if (args.Cancelled || !args.ProcessUserInput)
            return;

        ApplySanitization(ent, ref args);

        if (args.Cancelled)
            return;

        SpamCheck(ent, ref args);
    }

    private void ApplySanitization(Entity<ActorComponent> ent, ref TrySendChatMessageEvent args)
    {
        if (!_chatSanitizationEnabled)
            return;

        if (!_player.TryGetSessionByEntity(ent, out var session))
            return;

        if (_aggressive)
            HandleAggressiveSanitization(session, ref args);
        else
            HandleSoftSanitization(ref args);
    }

    private void HandleAggressiveSanitization(ICommonSession session, ref TrySendChatMessageEvent args)
    {
        if (!ContainsProhibitedContent(args.Message, out var reason))
            return;

        NotifyAdminsAboutBlockedMessage(session, args.Message, reason);
        args.Cancelled = true;
    }

    private static void HandleSoftSanitization(ref TrySendChatMessageEvent args)
    {
        args.Message = StripProhibitedCharacters(args.Message);

        if (MightContainUrl(args.Message))
            args.Message = UrlRegex().Replace(args.Message, string.Empty);
    }

    private bool ContainsProhibitedContent(string message, [NotNullWhen(true)] out string? reason)
    {
        reason = null;

        if (MightContainUrl(message) && UrlRegex().IsMatch(message))
        {
            reason = Loc.GetString("chatsan-blocked-reason-url");
            return true;
        }

        if (ContainsProhibitedCharacters(message))
        {
            reason = Loc.GetString("chatsan-blocked-reason-ascii-art");
            return true;
        }

        return false;
    }

    private void NotifyAdminsAboutBlockedMessage(ICommonSession session, string message, string reason)
    {
        var cropped = message.Length > MaxAdminAlertMessageLength
            ? message[..MaxAdminAlertMessageLength]
            : message;

        _chat.SendAdminAlert(Loc.GetString(
            "chatsan-admin-alert",
            ("user", session.Data.UserName),
            ("reason", reason),
            ("message_cropped", cropped)
        ));
    }

    private static bool MightContainUrl(string message)
    {
        return message.Contains('.') || message.Contains("://");
    }

    private static bool ContainsProhibitedCharacters(string message)
    {
        for (var i = 0; i < message.Length; i++)
        {
            if (IsProhibitedCharacter(message[i]))
                return true;
        }

        return false;
    }

    private static string StripProhibitedCharacters(string message)
    {
        var firstBlockedIndex = -1;
        for (var i = 0; i < message.Length; i++)
        {
            if (!IsProhibitedCharacter(message[i]))
                continue;

            firstBlockedIndex = i;
            break;
        }

        if (firstBlockedIndex < 0)
            return message;

        var builder = new StringBuilder(message.Length);
        builder.Append(message, 0, firstBlockedIndex);

        for (var i = firstBlockedIndex + 1; i < message.Length; i++)
        {
            var ch = message[i];
            if (!IsProhibitedCharacter(ch))
                builder.Append(ch);
        }

        return builder.ToString();
    }

    private static bool IsProhibitedCharacter(char ch)
    {
        if (ch is '卐' or '卍')
            return true;

        if (ch is '\t' or '\n' or '\r')
            return false;

        var category = char.GetUnicodeCategory(ch);
        if (category is UnicodeCategory.Control
            or UnicodeCategory.Format
            or UnicodeCategory.Surrogate
            or UnicodeCategory.PrivateUse
            or UnicodeCategory.OtherNotAssigned
            or UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark)
        {
            return true;
        }

        if (category != UnicodeCategory.OtherSymbol)
            return false;

        return ch != '№';
    }
}
