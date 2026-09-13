using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.Administration;
using Robust.Server.ServerStatus;
using Robust.Shared.Asynchronous;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Administration.Managers;

public sealed partial class AdminManager
{
    private const string StellarAdminWebhookPath = "/internal/stellar-admin/rbac-changed";
    private static readonly JsonSerializerOptions StellarAdminJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Dependency] private IStatusHost _stellarStatusHost = default!;
    [Dependency] private ITaskManager _stellarTaskManager = default!;

    private readonly HttpClient _stellarAdminHttpClient = new();
    private readonly object _stellarAdminCacheLock = new();
    private readonly Dictionary<StellarAdminCacheKey, StellarAdminCacheEntry> _stellarAdminCache = new();

    private void InitializeSunriseAdmin()
    {
        _stellarStatusHost.AddHandler(HandleStellarAdminRbacChanged);
    }

    internal bool UsesSunriseExternalAdminPermissions()
    {
        return _cfg.GetCVar(SunriseCCVars.StellarAdminEnabled);
    }

    private bool IsSunriseSpecialLogin(ICommonSession session)
    {
        return _admins.TryGetValue(session, out var reg) && reg.IsSpecialLogin;
    }

    internal bool IsSunriseAdminPermissionsUiBlocked(ICommonSession? session)
    {
        return UsesSunriseExternalAdminPermissions() &&
               session != null &&
               !IsSunriseSpecialLogin(session);
    }

    private bool ShouldPersistSunriseDeadminState(ICommonSession session)
    {
        return !IsSunriseAdminPermissionsUiBlocked(session);
    }

    private bool ShouldKeepSunriseRuntimeDeadminState(bool specialLogin)
    {
        return !specialLogin && UsesSunriseExternalAdminPermissions();
    }

    private async Task<(AdminData dat, int? rankId, bool specialLogin)?> TryLoadSunriseExternalAdminData(NetUserId userId)
    {
        if (!UsesSunriseExternalAdminPermissions())
            return null;

        var projectSlug = GetStellarAdminProjectSlug();
        var now = DateTimeOffset.UtcNow;
        var key = new StellarAdminCacheKey(userId, projectSlug);

        lock (_stellarAdminCacheLock)
        {
            if (_stellarAdminCache.TryGetValue(key, out var cached) && cached.ExpiresAt > now)
                return BuildStellarAdminData(cached.Response);
        }

        try
        {
            var response = await FetchStellarAdminPermissions(userId, projectSlug);
            var ttl = GetStellarAdminCacheTtl();
            var entry = new StellarAdminCacheEntry(
                response,
                now + ttl,
                now + GetStellarAdminMaxStale(ttl));

            lock (_stellarAdminCacheLock)
            {
                _stellarAdminCache[key] = entry;
            }

            if (!response.IsAdmin)
            {
                _sawmill.Info($"stellar-admin: {userId} has no admin permissions for {projectSlug}");
                return null;
            }

            _sawmill.Info($"stellar-admin: loaded permissions for {userId}: {string.Join(", ", response.Flags)}");
            return BuildStellarAdminData(response);
        }
        catch (Exception ex)
        {
            _sawmill.Warning($"stellar-admin: failed to load permissions for {userId}: {ex.Message}");

            lock (_stellarAdminCacheLock)
            {
                if (!_stellarAdminCache.TryGetValue(key, out var cached) || !cached.Response.IsAdmin)
                    return null;

                if (_cfg.GetCVar(SunriseCCVars.StellarAdminFailClosed))
                    return null;

                if (cached.MaxStaleAt <= now)
                    return null;

                _sawmill.Warning($"stellar-admin: using stale permissions for {userId}");
                return BuildStellarAdminData(cached.Response);
            }
        }
    }

    private async Task<StellarAdminPermissionsResponse> FetchStellarAdminPermissions(NetUserId userId, string projectSlug)
    {
        var apiUrl = _cfg.GetCVar(SunriseCCVars.StellarAdminApiUrl).Trim().TrimEnd('/');
        var token = _cfg.GetCVar(SunriseCCVars.StellarAdminApiToken).Trim();
        if (string.IsNullOrWhiteSpace(apiUrl) || string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("stellar_admin.api_url or stellar_admin.api_token is not configured");

        var requestUrl =
            $"{apiUrl}/api/internal/projects/{Uri.EscapeDataString(projectSlug)}/gameserver/admin-permissions" +
            $"?user_id={Uri.EscapeDataString(userId.UserId.ToString())}";
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        using var response = await _stellarAdminHttpClient.SendAsync(request, cancellation.Token);
        var body = await response.Content.ReadAsStringAsync(cancellation.Token);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"HTTP {(int) response.StatusCode}: {body}");

        var payload = JsonSerializer.Deserialize<StellarAdminPermissionsResponse>(body, StellarAdminJsonOptions);
        if (payload == null)
            throw new InvalidOperationException("stellar-admin response body is empty");

        return payload;
    }

    private (AdminData dat, int? rankId, bool specialLogin)? BuildStellarAdminData(StellarAdminPermissionsResponse response)
    {
        if (!response.IsAdmin)
            return null;

        var flags = AdminFlags.None;
        foreach (var rawFlag in response.Flags)
        {
            if (Enum.TryParse<AdminFlags>(rawFlag, ignoreCase: true, out var flag) && flag != AdminFlags.None)
            {
                flags |= flag;
                continue;
            }

            _sawmill.Warning($"stellar-admin: unknown AdminFlags value '{rawFlag}' in response");
        }

        if (flags == AdminFlags.None)
            return null;

        var data = new AdminData
        {
            Title = string.IsNullOrWhiteSpace(response.Title) ? "Stellar Admin" : response.Title,
            Flags = flags,
            Active = true,
        };
        return (data, null, false);
    }

    private TimeSpan GetStellarAdminCacheTtl()
    {
        return TimeSpan.FromSeconds(Math.Max(_cfg.GetCVar(SunriseCCVars.StellarAdminCacheTtlSeconds), 1));
    }

    private static TimeSpan GetStellarAdminMaxStale(TimeSpan ttl)
    {
        return TimeSpan.FromSeconds(Math.Max(ttl.TotalSeconds * 12, 3600));
    }

    private string GetStellarAdminProjectSlug()
    {
        var projectSlug = _cfg.GetCVar(SunriseCCVars.StellarAdminProjectSlug).Trim();
        return string.IsNullOrWhiteSpace(projectSlug) ? "test-sunrise-station" : projectSlug;
    }

    private void InvalidateStellarAdminCache(NetUserId userId, string projectSlug)
    {
        lock (_stellarAdminCacheLock)
        {
            _stellarAdminCache.Remove(new StellarAdminCacheKey(userId, projectSlug));
        }
    }

    private async Task<bool> HandleStellarAdminRbacChanged(IStatusHandlerContext context)
    {
        if (context.Url.AbsolutePath != StellarAdminWebhookPath)
            return false;

        if (context.RequestMethod != HttpMethod.Post)
        {
            await context.RespondErrorAsync(HttpStatusCode.MethodNotAllowed);
            return true;
        }

        if (!CheckStellarAdminWebhookAuth(context))
        {
            _sawmill.Warning($"stellar-admin: unauthorized RBAC webhook from {context.RemoteEndPoint}");
            await context.RespondErrorAsync(HttpStatusCode.Unauthorized);
            return true;
        }

        if (!CheckStellarAdminWebhookClientIp(context, out var resolvedClientIp))
        {
            _sawmill.Warning($"stellar-admin: rejected RBAC webhook from {context.RemoteEndPoint}, resolved client IP {resolvedClientIp}");
            await context.RespondErrorAsync(HttpStatusCode.Forbidden);
            return true;
        }

        StellarAdminRbacChangedPayload? payload;
        try
        {
            payload = await context.RequestBodyJsonAsync<StellarAdminRbacChangedPayload>();
        }
        catch (Exception ex)
        {
            _sawmill.Warning($"stellar-admin: invalid RBAC webhook payload: {ex.Message}");
            await context.RespondErrorAsync(HttpStatusCode.BadRequest);
            return true;
        }

        if (payload == null ||
            !TryValidateStellarAdminRbacChangedPayload(
                payload.Project,
                payload.UserId,
                payload.Revision,
                out var payloadProject,
                out var userGuid))
        {
            await context.RespondErrorAsync(HttpStatusCode.BadRequest);
            return true;
        }

        var projectSlug = GetStellarAdminProjectSlug();
        if (!string.Equals(payloadProject, projectSlug, StringComparison.OrdinalIgnoreCase))
        {
            await context.RespondJsonAsync(new { ok = true, ignored = true });
            return true;
        }

        var userId = new NetUserId(userGuid);
        InvalidateStellarAdminCache(userId, projectSlug);

        _stellarTaskManager.RunOnMainThread(() =>
        {
            if (_playerManager.TryGetSessionById(userId, out var session))
                ReloadAdmin(session);
        });

        await context.RespondJsonAsync(new { ok = true });
        return true;
    }

    internal static bool TryValidateStellarAdminRbacChangedPayload(
        string? project,
        string? userId,
        string? revision,
        out string projectSlug,
        out Guid userGuid)
    {
        projectSlug = (project ?? string.Empty).Trim();
        var userIdValue = (userId ?? string.Empty).Trim();
        var revisionValue = (revision ?? string.Empty).Trim();
        userGuid = Guid.Empty;

        if (string.IsNullOrWhiteSpace(projectSlug) ||
            string.IsNullOrWhiteSpace(userIdValue) ||
            string.IsNullOrWhiteSpace(revisionValue))
        {
            return false;
        }

        return Guid.TryParse(userIdValue, out userGuid);
    }

    private bool CheckStellarAdminWebhookClientIp(IStatusHandlerContext context, out IPAddress resolvedClientIp)
    {
        return IsStellarAdminWebhookClientIpAllowed(
            _cfg.GetCVar(SunriseCCVars.StellarAdminWebhookIpAllowlistEnabled),
            _cfg.GetCVar(SunriseCCVars.StellarAdminWebhookIpAllowlist),
            _cfg.GetCVar(SunriseCCVars.StellarAdminWebhookTrustedProxyCidrs),
            context.RemoteEndPoint.Address,
            GetStellarAdminHeaderValue(context, "X-Forwarded-For"),
            GetStellarAdminHeaderValue(context, "Forwarded"),
            out resolvedClientIp);
    }

    internal static bool IsStellarAdminWebhookClientIpAllowed(
        bool allowlistEnabled,
        string? allowlist,
        string? trustedProxyCidrs,
        IPAddress remoteIp,
        string? xForwardedFor,
        string? forwarded,
        out IPAddress resolvedClientIp)
    {
        resolvedClientIp = NormalizeStellarAdminIp(remoteIp);

        if (IsStellarAdminTrustedProxy(resolvedClientIp, trustedProxyCidrs) &&
            TryGetStellarAdminForwardedClientIp(xForwardedFor, forwarded, out var forwardedClientIp))
        {
            resolvedClientIp = forwardedClientIp;
        }

        if (!allowlistEnabled)
            return true;

        if (!TryParseStellarAdminIpRanges(allowlist, requireAny: true, out var allowlistRanges))
            return false;

        foreach (var range in allowlistRanges)
        {
            if (range.Contains(resolvedClientIp))
                return true;
        }

        return false;
    }

    private static bool IsStellarAdminTrustedProxy(IPAddress remoteIp, string? trustedProxyCidrs)
    {
        if (!TryParseStellarAdminIpRanges(trustedProxyCidrs, requireAny: false, out var trustedProxyRanges))
            return false;

        foreach (var range in trustedProxyRanges)
        {
            if (range.Contains(remoteIp))
                return true;
        }

        return false;
    }

    private static bool TryParseStellarAdminIpRanges(
        string? rawRanges,
        bool requireAny,
        out List<StellarAdminIpRange> ranges)
    {
        ranges = new List<StellarAdminIpRange>();

        foreach (var rawRange in (rawRanges ?? string.Empty)
                     .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryParseStellarAdminIpRange(rawRange, out var range))
                return false;

            ranges.Add(range);
        }

        return !requireAny || ranges.Count > 0;
    }

    private static bool TryParseStellarAdminIpRange(string rawRange, out StellarAdminIpRange range)
    {
        range = default;
        var parts = rawRange.Split('/', 2, StringSplitOptions.TrimEntries);
        if (parts.Length is 0 || !TryParseStellarAdminPlainIp(parts[0], out var network))
            return false;

        var maxPrefixLength = GetStellarAdminMaxPrefixLength(network);
        var prefixLength = maxPrefixLength;
        if (parts.Length == 2 &&
            (!int.TryParse(parts[1], out prefixLength) ||
             prefixLength < 0 ||
             prefixLength > maxPrefixLength))
        {
            return false;
        }

        range = new StellarAdminIpRange(network, prefixLength);
        return true;
    }

    private static bool TryGetStellarAdminForwardedClientIp(
        string? xForwardedFor,
        string? forwarded,
        out IPAddress clientIp)
    {
        clientIp = IPAddress.None;

        if (!string.IsNullOrWhiteSpace(xForwardedFor))
        {
            foreach (var rawForwardedFor in xForwardedFor.Split(','))
            {
                if (string.IsNullOrWhiteSpace(rawForwardedFor))
                    continue;

                return TryParseStellarAdminForwardedIp(rawForwardedFor, out clientIp);
            }
        }

        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            foreach (var forwardedEntry in forwarded.Split(','))
            {
                foreach (var forwardedPart in forwardedEntry.Split(';'))
                {
                    var part = forwardedPart.Trim();
                    if (!part.StartsWith("for=", StringComparison.OrdinalIgnoreCase))
                        continue;

                    return TryParseStellarAdminForwardedIp(part[4..], out clientIp);
                }
            }
        }

        return false;
    }

    private static bool TryParseStellarAdminForwardedIp(string rawIp, out IPAddress ip)
    {
        ip = IPAddress.None;
        var value = rawIp.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(value) || value.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            return false;

        if (value.StartsWith("[", StringComparison.Ordinal))
        {
            var closingBracket = value.IndexOf(']');
            return closingBracket > 1 &&
                   TryParseStellarAdminPlainIp(value[1..closingBracket], out ip);
        }

        if (TryParseStellarAdminPlainIp(value, out ip))
            return true;

        var lastColon = value.LastIndexOf(':');
        if (lastColon > 0 && value.IndexOf(':') == lastColon)
            return TryParseStellarAdminPlainIp(value[..lastColon], out ip);

        return false;
    }

    private static bool TryParseStellarAdminPlainIp(string rawIp, out IPAddress ip)
    {
        if (!IPAddress.TryParse(rawIp.Trim(), out var parsedIp))
        {
            ip = IPAddress.None;
            return false;
        }

        ip = NormalizeStellarAdminIp(parsedIp);
        return ip.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6;
    }

    private static IPAddress NormalizeStellarAdminIp(IPAddress ip)
    {
        return ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;
    }

    private static int GetStellarAdminMaxPrefixLength(IPAddress ip)
    {
        return ip.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
    }

    private static string? GetStellarAdminHeaderValue(IStatusHandlerContext context, string headerName)
    {
        return context.RequestHeaders.TryGetValue(headerName, out var values) && values.Count > 0
            ? values[0]
            : null;
    }

    private bool CheckStellarAdminWebhookAuth(IStatusHandlerContext context)
    {
        var token = _cfg.GetCVar(SunriseCCVars.StellarAdminWebhookToken).Trim();
        if (string.IsNullOrWhiteSpace(token))
            return false;

        return context.RequestHeaders.TryGetValue("Authorization", out var authValues) &&
               authValues.Count > 0 &&
               authValues[0] == $"Bearer {token}";
    }

    private readonly record struct StellarAdminCacheKey(NetUserId UserId, string ProjectSlug);

    private readonly record struct StellarAdminCacheEntry(
        StellarAdminPermissionsResponse Response,
        DateTimeOffset ExpiresAt,
        DateTimeOffset MaxStaleAt);

    private readonly record struct StellarAdminIpRange(IPAddress Network, int PrefixLength)
    {
        public bool Contains(IPAddress ip)
        {
            var network = NormalizeStellarAdminIp(Network);
            var candidate = NormalizeStellarAdminIp(ip);
            if (network.AddressFamily != candidate.AddressFamily)
                return false;

            var networkBytes = network.GetAddressBytes();
            var candidateBytes = candidate.GetAddressBytes();
            var fullBytes = PrefixLength / 8;
            var remainderBits = PrefixLength % 8;

            for (var i = 0; i < fullBytes; i++)
            {
                if (networkBytes[i] != candidateBytes[i])
                    return false;
            }

            if (remainderBits == 0)
                return true;

            var mask = (byte) (0xff << (8 - remainderBits));
            return (networkBytes[fullBytes] & mask) == (candidateBytes[fullBytes] & mask);
        }
    }

    private sealed record StellarAdminPermissionsResponse(
        string Project,
        string UserId,
        bool IsAdmin,
        string? Title,
        string[] RoleCodes,
        string[] Flags,
        string Version,
        int ExpiresInSeconds);

    private sealed record StellarAdminRbacChangedPayload(
        string Project,
        string UserId,
        string Revision);
}
