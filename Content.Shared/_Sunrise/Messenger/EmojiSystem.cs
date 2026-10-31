using System.Collections.Frozen;
using System.Text.RegularExpressions;
using Content.Shared.Chat;
using Content.Sunrise.Interfaces.Shared;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Messenger;

/// <summary>
/// System for handling messenger emojis.
/// </summary>
public abstract partial class SharedEmojiSystem : EntitySystem
{

    private const ChatChannel EmojiSupportedChannels =
        ChatChannel.OOC
        | ChatChannel.LOOC
        | ChatChannel.Dead
        | ChatChannel.AdminRelated
        | ChatChannel.Server;

    public FrozenDictionary<string, EmojiPrototype> Emojis { get; private set; } =
        FrozenDictionary<string, EmojiPrototype>.Empty;

    private static readonly Regex EmojiRegex = new(
        @"(?<![a-zA-Z0-9_]):[^\s:]+:(?![a-zA-Z0-9_])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypeReload);

        CollectEmojis();
    }

    private void OnPrototypeReload(PrototypesReloadedEventArgs args)
    {
        CollectEmojis();
    }

    /// <summary>
    /// Parses message text and replaces emoji codes with RichTextLabel format.
    /// </summary>
    public string ParseEmojis(string text)
    {
        return EmojiRegex.Replace(text, match =>
        {
            if (Emojis.TryGetValue(match.Value, out var emoji))
                return $"[emoji id=\"{emoji.ID}\"]";

            return match.Value;
        });
    }

    /// <summary>
    /// Filters emoji codes the user doesn't have access to.
    /// </summary>
    public string FilterBlockedEmojis(string text, NetUserId userId, ISharedSponsorsManager? sponsorsManager)
    {
        if (!IsContainsAnyEmoji(text))
            return text;

        return EmojiRegex.Replace(text, match =>
        {
            if (Emojis.TryGetValue(match.Value, out var emoji))
            {
                if (emoji.SponsorOnly)
                {
                    var hasAccess = false;
                    if (sponsorsManager != null)
                    {
                        if (sponsorsManager.TryGetPrototypes(userId, out var prototypes))
                        {
                            hasAccess = prototypes.Contains(emoji.ID);
                        }
                    }

                    if (!hasAccess)
                    {
                        return $":\u200b{match.Value.Trim(':')}\u200b:";
                    }
                }
            }

            return match.Value;
        });
    }

    private void CollectEmojis()
    {
        Emojis = ProtoMan.EnumeratePrototypes<EmojiPrototype>()
            .ToFrozenDictionary(e => e.Code, e => e);
    }

    /// <summary>
    /// Checks if string has a potential emoji.
    /// Fast check for early return.
    /// Prevents heavier regex validation if no emojis are present.
    /// </summary>
    /// <param name="text">Text to check</param>
    /// <returns>Whether a potential emoji is present.</returns>
    /// <remarks>
    /// Slightly faster than regex.
    /// </remarks>
    public static bool IsContainsAnyEmoji(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length < 3)
            return false;

        var firstColon = text.IndexOf(':');
        if (firstColon == -1)
            return false; // Нет ни одного двоеточия

        var lastColon = text.LastIndexOf(':');

        // Вернет true, если двоеточий больше одного и между ними есть хотя бы 1 символ
        return lastColon > firstColon + 1;
    }

    public static bool IsEmojiAllowedInChannel(ChatChannel channel)
    {
        return channel == ChatChannel.None || (channel & EmojiSupportedChannels) != 0;
    }

    public bool IsEmojiAllowedForPlayer(string emojiId, NetUserId userId, ISharedSponsorsManager? sponsorsManager)
    {
        if (!ProtoMan.TryIndex<EmojiPrototype>(emojiId, out var emoji))
            return false;

        if (!emoji.SponsorOnly)
            return true;

        if (sponsorsManager == null)
            return false;

        if (sponsorsManager.TryGetPrototypes(userId, out var prototypes))
        {
            return prototypes.Contains(emojiId);
        }

        return false;
    }
}
