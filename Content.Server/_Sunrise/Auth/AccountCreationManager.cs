using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Discord;
using Content.Shared.CCVar;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Network;

namespace Content.Server._Sunrise.Auth;

public sealed partial class AccountCreationManager
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private DiscordWebhook _discord = default!;
    private HttpClient _httpClient = default!;
    private ISawmill _sawmill = default!;
    private string _api = "api/query/userid?userid=";
    public void Initialize()
    {
        _sawmill = _logManager.GetSawmill("auth_created_time_api");
        _httpClient = _discord.GetClient();
    }
    public async Task<DateTimeOffset?> TryGetAccountCreatedTimeAsync(NetUserId userId, CancellationToken cancel = default)
    {
        try
        {
            var authServer = _cfg.GetCVar(CVars.AuthServer);
            var requestUri = $"{authServer}{_api}{WebUtility.UrlEncode(userId.UserId.ToString())}";
            using var resp = await _httpClient.GetAsync(requestUri, cancel);

            if (resp.StatusCode == HttpStatusCode.NotFound)
                return null;

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                _sawmill.Error("Auth server token is invalid.");
                return null;
            }

            if (!resp.IsSuccessStatusCode)
            {
                _sawmill.Error("Auth server returned bad response {StatusCode}!", resp.StatusCode);
                return null;
            }

            var responseData = await resp.Content.ReadFromJsonAsync<UserDataResponse>(cancellationToken: cancel);
            return responseData?.CreatedTime;
        }
        catch (HttpRequestException e)
        {
            _sawmill.Error($"Auth server request failed: {e}");
        }

        return null;
    }

    private sealed record UserDataResponse(
        string UserName,
        Guid UserId,
        int? PatronTier,
        DateTimeOffset CreatedTime);
}
