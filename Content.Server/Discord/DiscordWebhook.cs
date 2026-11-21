using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Server.Discord;

public sealed partial class DiscordWebhook : IPostInjectInit, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    [Dependency] private ILogManager _log = default!;
    [Dependency] private IConfigurationManager _configuration = default!;

    private const string BaseUrl = "https://discord.com/api/v10/webhooks";
    private HttpClient _http = default!;
    private ISawmill _sawmill = default!;

    // Sunrise added start
    private const char CustomHeadersSplitter = '|';
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30f);
    // Sunrise added end

    private string GetUrl(WebhookIdentifier identifier)
    {
        return $"{BaseUrl}/{identifier.Id}/{identifier.Token}";
    }

    /// <summary>
    ///     Gets the webhook data from the given webhook url.
    /// </summary>
    /// <param name="url">The url to get the data from.</param>
    /// <returns>The webhook data returned from the url.</returns>
    public async Task<WebhookData?> GetWebhook(string url)
    {
        try
        {
            return await _http.GetFromJsonAsync<WebhookData>(url);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Error getting discord webhook data.\n{e}");
            return null;
        }
    }

    /// <summary>
    ///     Gets the webhook data from the given webhook url.
    /// </summary>
    /// <param name="url">The url to get the data from.</param>
    /// <param name="onComplete">The delegate to invoke with the obtained data, if any.</param>
    public async void GetWebhook(string url, Action<WebhookData> onComplete)
    {
        if (await GetWebhook(url) is { } data)
            onComplete(data);
    }

    /// <summary>
    ///     Tries to get the webhook data from the given webhook url if it is not null or whitespace.
    /// </summary>
    /// <param name="url">The url to get the data from.</param>
    /// <param name="onComplete">The delegate to invoke with the obtained data, if any.</param>
    public async void TryGetWebhook(string url, Action<WebhookData> onComplete)
    {
        if (await GetWebhook(url) is { } data)
            onComplete(data);
    }

    /// <summary>
    ///     Creates a new webhook message with the given identifier and payload.
    /// </summary>
    /// <param name="identifier">The identifier for the webhook url.</param>
    /// <param name="payload">The payload to create the message from.</param>
    /// <returns>The response from Discord's API.</returns>
    public async Task<HttpResponseMessage> CreateMessage(WebhookIdentifier identifier, WebhookPayload payload)
    {
        var url = $"{GetUrl(identifier)}?wait=true";
        var response = await _http.PostAsJsonAsync(url, payload, JsonOptions);

        LogResponse(response, "Create");

        return response;
    }

    /// <summary>
    ///     Deletes a webhook message with the given identifier and message id.
    /// </summary>
    /// <param name="identifier">The identifier for the webhook url.</param>
    /// <param name="messageId">The message id to delete.</param>
    /// <returns>The response from Discord's API.</returns>
    public async Task<HttpResponseMessage> DeleteMessage(WebhookIdentifier identifier, ulong messageId)
    {
        var url = $"{GetUrl(identifier)}/messages/{messageId}";
        var response = await _http.DeleteAsync(url);

        LogResponse(response, "Delete");

        return response;
    }

    /// <summary>
    ///     Creates a new webhook message with the given identifier, message id and payload.
    /// </summary>
    /// <param name="identifier">The identifier for the webhook url.</param>
    /// <param name="messageId">The message id to edit.</param>
    /// <param name="payload">The payload used to edit the message.</param>
    /// <returns>The response from Discord's API.</returns>
    public async Task<HttpResponseMessage> EditMessage(WebhookIdentifier identifier, ulong messageId, WebhookPayload payload)
    {
        var url = $"{GetUrl(identifier)}/messages/{messageId}";
        var response = await _http.PatchAsJsonAsync(url, payload, JsonOptions);

        LogResponse(response, "Edit");

        return response;
    }

    void IPostInjectInit.PostInject()
    {
        _sawmill = _log.GetSawmill("DISCORD");
    }

    // Sunrise edit start
    // Тут добавлена поддержка прокси и кастомных заголовков. РКН сосать
    #region HTTP client

    public void SetupClient()
    {
        _http = CreateHttpClient();
    }

    public HttpClient GetClient()
    {
        return _http;
    }

    private HttpClient CreateHttpClient()
    {
        HttpClient client;
        var proxyAddress = _configuration.GetCVar(SunriseCCVars.DiscordProxyAddress);

        if (string.IsNullOrWhiteSpace(proxyAddress))
        {
            client = new HttpClient();
            _sawmill.Debug("No proxy configured for Discord webhooks.");
        }
        else
        {
            try
            {
                client = CreateClientWithProxies(proxyAddress);
                _sawmill.Info("Configured Discord webhooks to use proxies");
            }
            catch (Exception ex)
            {
                _sawmill.Error($"Failed to configure proxy for Discord webhooks: {ex.Message}. Continuing without proxy.");
                client = new HttpClient();
                client.Timeout = Timeout;
            }
        }

        // Add custom headers if configured
        TryAddCustomHeaders(client);

        return client;
    }

    private HttpClient CreateClientWithProxies(string proxyAddress)
    {
        var handler = CreateHandler(proxyAddress);
        if (handler == null)
        {
            throw new InvalidOperationException("Failed to create proxy handler");
        }

        var client = new HttpClient(handler);
        client.Timeout = Timeout;

        return client;
    }

    public SocketsHttpHandler? CreateHandler(string proxyAddress)
    {
        if (!Uri.TryCreate(proxyAddress, UriKind.Absolute, out var proxyUri))
        {
            _sawmill.Error($"Invalid proxy URI: {proxyAddress}. Discord connections will continue without proxy.");
            return null;
        }

        var proxyUsername = _configuration.GetCVar(SunriseCCVars.DiscordProxyUsername);
        var proxyPassword = _configuration.GetCVar(SunriseCCVars.DiscordProxyPassword);

        var handler = new SocketsHttpHandler();

        NetworkCredential? credentials = null;
        if (!string.IsNullOrWhiteSpace(proxyUsername) && !string.IsNullOrWhiteSpace(proxyPassword))
            credentials = new NetworkCredential(proxyUsername, proxyPassword);

        var proxy = new WebProxy(proxyUri, false, null, credentials)
        {
            UseDefaultCredentials = false,
        };

        handler.Proxy = proxy;
        handler.UseProxy = true;
        handler.ConnectTimeout = Timeout;

        return handler;
    }

    private bool TryAddCustomHeaders(HttpClient client)
    {
        var customHeaders = _configuration.GetCVar(SunriseCCVars.DiscordCustomHeaders);
        if (string.IsNullOrWhiteSpace(customHeaders))
            return false;

        var usedHeadersCount = 0;
        var headers = customHeaders.Split(CustomHeadersSplitter);
        foreach (var header in headers)
        {
            var parts = header.Trim().Split(':', 2);
            if (parts.Length != 2)
            {
                _sawmill.Error($"Invalid header: {header}");
                continue;
            }

            var headerName = parts[0].Trim();
            var headerValue = parts[1].Trim();

            if (string.IsNullOrWhiteSpace(headerName))
            {
                _sawmill.Error($"Empty header name in: {header}");
                continue;
            }

            client.DefaultRequestHeaders.Add(headerName, headerValue);
            usedHeadersCount++;
        }

        if (usedHeadersCount == 0)
            return false;

        _sawmill.Info($"Configured Discord webhooks with {usedHeadersCount} custom headers.");
        return true;
    }
    #endregion

    public void Dispose()
    {
        _http?.Dispose();
    }
    // Sunrise edit end

    /// <summary>
    ///     Logs detailed information about the HTTP response received from a Discord webhook request.
    ///     If the response status code is non-2XX it logs the status code, relevant rate limit headers.
    /// </summary>
    /// <param name="response">The HTTP response received from the Discord API.</param>
    /// <param name="methodName">The name (constant) of the method that initiated the webhook request (e.g., "Create", "Edit", "Delete").</param>
    private void LogResponse(HttpResponseMessage response, string methodName)
    {
        if (!response.IsSuccessStatusCode)
        {
            _sawmill.Error($"Failed to {methodName} message. Status code: {response.StatusCode}.");

            if (response.Headers.TryGetValues("Retry-After", out var retryAfter))
                _sawmill.Debug($"Failed webhook response Retry-After: {string.Join(", ", retryAfter)}");

            if (response.Headers.TryGetValues("X-RateLimit-Global", out var globalRateLimit))
                _sawmill.Debug($"Failed webhook response X-RateLimit-Global: {string.Join(", ", globalRateLimit)}");

            if (response.Headers.TryGetValues("X-RateLimit-Scope", out var rateLimitScope))
                _sawmill.Debug($"Failed webhook response X-RateLimit-Scope: {string.Join(", ", rateLimitScope)}");
        }
    }


}
