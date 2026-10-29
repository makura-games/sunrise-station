using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Server.Discord;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.Database;
using Robust.Shared;
using Robust.Shared.Log;
using Robust.Shared.Network;
#if SUNRISE_PRIVATE
using Content.Server._SunrisePrivate.MakuraAuth;
#endif

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Administration.Managers;

public sealed partial class BanManager
{
    [Dependency] private DiscordWebhook _discord = default!;

    private HttpClient _sunriseBanWebhookHttpClient = default!;
    private string _sunriseBanServerName = string.Empty;
    private string _sunriseBanWebhookUrl = string.Empty;
    private const string SunriseBanWebhookName = "Sunrise Ban";
    private const string SunriseBanWebhookAvatarUrl = "https://i.ibb.co/WfGqKtG/avatar.png";

    private static readonly Regex SunriseBanWebhookUrlRegex = new(@"^https://discord\.com/api/webhooks/(\d+)/((?!.*?/).*)$");

#if SUNRISE_PRIVATE
    private readonly HttpClient _sunriseBanIdentityHttpClient = new();
    private MakuraAccountIdentityResolver? _sunriseBanIdentityResolver;
#endif

    private string _sunriseBanIdentityApiUrl = string.Empty;
    private string _sunriseBanIdentityApiKey = string.Empty;

    private void InitializeSunriseBanWebhook()
    {
        _sunriseBanWebhookHttpClient = _discord.GetClient();
        InitializeSunriseBanWebhookHooks();
        _cfg.OnValueChanged(SunriseCCVars.DiscordBanWebhook, OnSunriseBanWebhookChanged, true);
        _cfg.OnValueChanged(Content.Shared.CCVar.CCVars.GameHostName, value => _sunriseBanServerName = value, true);
    }

    private void InitializeSunriseBanWebhookHooks()
    {
#if SUNRISE_PRIVATE
        _sunriseBanIdentityResolver = new MakuraAccountIdentityResolver(
            _sunriseBanIdentityHttpClient,
            _sawmill);
        _cfg.OnValueChanged(SunriseCCVars.MakuraAuthInternalApiUrl, value => _sunriseBanIdentityApiUrl = value, true);
        _cfg.OnValueChanged(SunriseCCVars.MakuraAuthInternalApiKey, value => _sunriseBanIdentityApiKey = value, true);
#endif
    }

    private async Task SendServerBanWebhookBestEffort(BanDef ban, uint? minutes)
    {
        try
        {
            await SendSunriseBanWebhook(await GenerateSunriseServerBanPayload(ban, minutes));
        }
        catch (Exception exception)
        {
            _sawmill.Warning("Failed to send server ban webhook: {Message}", exception.Message);
        }
    }

    private async Task SendRoleBanWebhookBestEffort(BanDef ban, uint? minutes)
    {
        try
        {
            await SendSunriseBanWebhook(await GenerateSunriseRoleBanPayload(ban, minutes));
        }
        catch (Exception exception)
        {
            _sawmill.Warning("Failed to send role ban webhook: {Message}", exception.Message);
        }
    }

    private async Task SendSunriseBanWebhook(SunriseWebhookPayload payload)
    {
        if (string.IsNullOrWhiteSpace(_sunriseBanWebhookUrl))
            return;

        var request = await _sunriseBanWebhookHttpClient.PostAsync(
            $"{_sunriseBanWebhookUrl}?wait=true",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
        var content = await request.Content.ReadAsStringAsync();

        if (!request.IsSuccessStatusCode)
        {
            _sawmill.Error(
                "Discord returned status {StatusCode} while posting ban webhook: {Response}",
                request.StatusCode,
                content);
            return;
        }

        if (JsonNode.Parse(content)?["id"] == null)
            _sawmill.Error("Discord ban webhook response did not contain a message id: {Response}", content);
    }

    private async Task<SunriseWebhookPayload> GenerateSunriseServerBanPayload(BanDef ban, uint? minutes)
    {
        var context = await GetSunriseBanWebhookContext(ban);
        var temporary = ban.ExpirationTime != null && minutes != null;
        var descriptionKey = temporary ? "server-time-ban-string" : "server-perma-ban-string";
        var author = temporary
            ? Loc.GetString("server-time-ban", ("mins", minutes!.Value)) + $" #{ban.Id}"
            : Loc.GetString("server-perma-ban") + $" #{ban.Id}";

        return CreateSunriseBanWebhookPayload(
            Loc.GetString(
                descriptionKey,
                ("targetName", context.TargetName),
                ("targetLink", context.Mentions.TargetLink),
                ("adminLink", context.Mentions.AdminLink),
                ("adminName", context.AdminName),
                ("TimeNow", context.TimeNow),
                ("expiresString", context.Expires),
                ("reason", ban.Reason),
                ("severity", context.Severity)),
            author,
            temporary ? 0x803045 : 0x8B0000,
            temporary
                ? "https://static.wikia.nocookie.net/ss14andromeda13/images/f/ff/Clown.png/revision/latest?cb=20230217121049&path-prefix=ru"
                : "https://static.wikia.nocookie.net/ss14andromeda13/images/7/72/%D0%94%D0%B5%D1%82%D0%B5%D0%BA%D1%82%D0%B8%D0%B2.png/revision/latest?cb=20230216091637&path-prefix=ru",
            context);
    }

    private async Task<SunriseWebhookPayload> GenerateSunriseRoleBanPayload(BanDef ban, uint? minutes)
    {
        var context = await GetSunriseBanWebhookContext(ban);
        var temporary = ban.ExpirationTime != null && minutes != null;
        var roles = string.Join(string.Empty, (ban.Roles ?? []).Select(role => $"\n> `{role.RoleId}`"));
        var descriptionKey = temporary ? "server-role-ban-string" : "server-perma-role-ban-string";
        var author = temporary
            ? Loc.GetString("server-role-ban", ("mins", minutes!.Value))
            : Loc.GetString("server-perma-role-ban");

        return CreateSunriseBanWebhookPayload(
            Loc.GetString(
                descriptionKey,
                ("targetName", context.TargetName),
                ("targetLink", context.Mentions.TargetLink),
                ("adminLink", context.Mentions.AdminLink),
                ("adminName", context.AdminName),
                ("TimeNow", context.TimeNow),
                ("roles", roles),
                ("expiresString", context.Expires),
                ("reason", ban.Reason),
                ("severity", context.Severity)),
            author,
            temporary ? 0x004281 : 0xffb840,
            temporary
                ? "https://static.wikia.nocookie.net/ss14andromeda13/images/6/66/%D0%9E%D1%84%D0%B8%D1%86%D0%B5%D1%80_%D0%A1%D0%BB%D1%83%D0%B6%D0%B1%D1%8B_%D0%91%D0%B5%D0%B7%D0%BE%D0%BF%D0%B0%D1%81%D0%BD%D0%BE%D1%81%D1%82%D0%B8.png/revision/latest/scale-to-width-down/110?cb=20230216091617&path-prefix=ru"
                : "https://static.wikia.nocookie.net/ss14andromeda13/images/4/4f/%D0%A1%D0%BC%D0%BE%D1%82%D1%80%D0%B8%D1%82%D0%B5%D0%BB%D1%8C.png/revision/latest?cb=20230216091556&path-prefix=ru",
            context);
    }

    private SunriseWebhookPayload CreateSunriseBanWebhookPayload(
        string description,
        string author,
        int color,
        string thumbnail,
        SunriseBanWebhookContext context)
    {
        return new SunriseWebhookPayload
        {
            Username = SunriseBanWebhookName,
            AvatarUrl = SunriseBanWebhookAvatarUrl,
            AllowedMentions = context.Mentions.AllowedMentions,
            Mentions = context.Mentions.Mentions,
            Embeds =
            [
                new SunriseWebhookEmbed
                {
                    Description = description,
                    Color = color,
                    Thumbnail = new SunriseWebhookThumbnail { Url = thumbnail },
                    Author = new SunriseWebhookAuthor
                    {
                        Name = author,
                        IconUrl = "https://cdn.discordapp.com/emojis/1129749368199712829.webp?size=40&quality=lossless",
                    },
                    Footer = new SunriseWebhookFooter
                    {
                        Text = Loc.GetString(
                            "server-ban-footer",
                            ("server", context.ServerName),
                            ("round", context.Round)),
                        IconUrl = "https://cdn.discordapp.com/emojis/1143995749928030208.webp?size=40&quality=lossless",
                    },
                },
            ],
        };
    }

    private async Task<SunriseBanWebhookContext> GetSunriseBanWebhookContext(BanDef ban)
    {
        var primaryUser = ban.UserIds.Length > 0 ? ban.UserIds[0] : (NetUserId?) null;
        var hwid = ban.HWIds.Length > 0
            ? string.Concat(ban.HWIds[0].Hwid.Select(value => value.ToString("x2")))
            : "null";
        var adminName = ban.BanningAdmin == null
            ? Loc.GetString("system-user")
            : (await _db.GetPlayerRecordByUserId(ban.BanningAdmin.Value))?.LastSeenUserName
              ?? Loc.GetString("system-user");

        var targetNames = new List<string>();
        foreach (var userId in ban.UserIds)
        {
            targetNames.Add(
                (await _db.GetPlayerRecordByUserId(userId))?.LastSeenUserName
                ?? Loc.GetString("server-ban-no-name", ("hwid", hwid)));
        }

        if (targetNames.Count == 0)
            targetNames.Add(Loc.GetString("server-ban-no-name", ("hwid", hwid)));

        var expires = ban.ExpirationTime == null
            ? Loc.GetString("server-ban-string-never")
            : TimeZoneInfo.ConvertTimeFromUtc(
                ban.ExpirationTime.Value.UtcDateTime,
                TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time")).ToString();
        var mentions = await GetSunriseBanWebhookMentions(ban.BanningAdmin, primaryUser);
        var serverName = _sunriseBanServerName[..Math.Min(_sunriseBanServerName.Length, 1500)];

        return new SunriseBanWebhookContext(
            string.Join(", ", targetNames),
            adminName,
            expires,
            string.Join(", ", ban.RoundIds),
            Loc.GetString($"admin-note-editor-severity-{ban.Severity.ToString().ToLowerInvariant()}"),
            serverName,
            TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time")),
            mentions);
    }

    private async Task<SunriseBanWebhookMentionData> GetSunriseBanWebhookMentions(
        NetUserId? adminId,
        NetUserId? targetId)
    {
        var adminDiscordIdTask = GetSunriseBanDiscordUserId(adminId);
        var targetDiscordIdTask = GetSunriseBanDiscordUserId(targetId);
        await Task.WhenAll(adminDiscordIdTask, targetDiscordIdTask);

        var adminLink = string.Empty;
        var targetLink = string.Empty;
        var mentions = new List<SunriseWebhookUser>();

        if (await adminDiscordIdTask is { } adminDiscordId)
        {
            adminLink = $"<@{adminDiscordId}>";
            mentions.Add(new SunriseWebhookUser { Id = adminDiscordId });
        }

        if (await targetDiscordIdTask is { } targetDiscordId)
        {
            targetLink = $"<@{targetDiscordId}>";
            mentions.Add(new SunriseWebhookUser { Id = targetDiscordId });
        }

        return new SunriseBanWebhookMentionData(
            adminLink,
            targetLink,
            mentions,
            new Dictionary<string, string[]> { { "parse", ["users"] } });
    }

    private async Task<string?> GetSunriseBanDiscordUserId(
        NetUserId? userId,
        CancellationToken cancel = default)
    {
#if SUNRISE_PRIVATE
        if (_sunriseBanIdentityResolver == null)
            return null;

        return await _sunriseBanIdentityResolver.GetDiscordUserId(
            userId,
            _sunriseBanIdentityApiUrl,
            _sunriseBanIdentityApiKey,
            cancel);
#else
        await Task.CompletedTask;
        return null;
#endif
    }

    private void OnSunriseBanWebhookChanged(string url)
    {
        _sunriseBanWebhookUrl = url;
        if (string.IsNullOrWhiteSpace(url))
            return;

        if (!SunriseBanWebhookUrlRegex.IsMatch(url))
            _sawmill.Warning("Discord ban webhook URL does not appear to be valid.");
    }

    private readonly record struct SunriseBanWebhookContext(
        string TargetName,
        string AdminName,
        string Expires,
        string Round,
        string Severity,
        string ServerName,
        DateTime TimeNow,
        SunriseBanWebhookMentionData Mentions);

    private readonly record struct SunriseBanWebhookMentionData(
        string AdminLink,
        string TargetLink,
        List<SunriseWebhookUser> Mentions,
        Dictionary<string, string[]> AllowedMentions);

    private struct SunriseWebhookPayload
    {
        [JsonPropertyName("username")]
        public string Username { get; set; }

        [JsonPropertyName("avatar_url")]
        public string AvatarUrl { get; set; }

        [JsonPropertyName("embeds")]
        public List<SunriseWebhookEmbed> Embeds { get; set; }

        [JsonPropertyName("mentions")]
        public List<SunriseWebhookUser> Mentions { get; set; }

        [JsonPropertyName("allowed_mentions")]
        public Dictionary<string, string[]> AllowedMentions { get; set; }
    }

    private struct SunriseWebhookEmbed
    {
        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("color")]
        public int Color { get; set; }

        [JsonPropertyName("author")]
        public SunriseWebhookAuthor Author { get; set; }

        [JsonPropertyName("thumbnail")]
        public SunriseWebhookThumbnail Thumbnail { get; set; }

        [JsonPropertyName("footer")]
        public SunriseWebhookFooter Footer { get; set; }
    }

    private struct SunriseWebhookAuthor
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("icon_url")]
        public string IconUrl { get; set; }
    }

    private struct SunriseWebhookThumbnail
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }
    }

    private struct SunriseWebhookFooter
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("icon_url")]
        public string IconUrl { get; set; }
    }

    private struct SunriseWebhookUser
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
    }
}
