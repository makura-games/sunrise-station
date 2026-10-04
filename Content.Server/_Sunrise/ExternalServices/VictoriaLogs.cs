using System.Net.Http;
using System.Text;
using System.Text.Json;
using Content.Server.Database;
using Content.Shared._Sunrise.SunriseCCVars;
using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Server._Sunrise.ExternalServices;

public sealed partial class VictoriaLogs : IPostInjectInit, IDisposable
{
    [Dependency] private ILogManager _log = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private ISawmill _sawmill = default!;
    private HttpClient? _client;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private bool _enabled;
    private string _baseAddress = string.Empty;
    private bool _storeInDatabase;
    private string _serverId = string.Empty;

    private const string InsertQuery = $"/insert/jsonline?_stream_fields={DefaultStreamValue}";

    public const string ServerId = "server_id";
    public const string LogSourceType = "log_source_type";
    private const string DefaultStreamValue = $"{ServerId},{LogSourceType}";

    void IPostInjectInit.PostInject()
    {
        _sawmill = _log.GetSawmill("VictoriaLogs");

        _cfg.OnValueChanged(SunriseCCVars.VictoriaLogsEnabled, OnEnabledChanged, true);
        _cfg.OnValueChanged(SunriseCCVars.VictoriaLogsBaseAddress, OnBaseAddressChanged, true);
        _cfg.OnValueChanged(SunriseCCVars.VictoriaLogsStoreInDatabase, OnDatabaseStoreChanged, true);
        _cfg.OnValueChanged(CVars.WatchdogKey, OnWatchdogKeyChanged, true);
    }

    public async void HandleDefaultAdminLog(AdminLog log)
    {
        if (!_enabled)
            return;

        var json = JsonSerializer.Serialize(new VictoriaAdminLog(log, _serverId), _jsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        _sawmill.Info(json);

        var uriString = _baseAddress + InsertQuery;
        if (!Uri.TryCreate(uriString, UriKind.Absolute, out var uri))
        {
            _sawmill.Error($"Failed to create URI for {uriString}");
            return;
        }

        var response = await _client!.PostAsync(uri, content);

        if (!response.IsSuccessStatusCode)
            _sawmill.Error(response.ReasonPhrase ?? $"Found unsuccessful request with {response.StatusCode} code");
    }

    public bool ShouldStoreLogInDatabase()
    {
        if (_enabled)
            return true;

        return _storeInDatabase;
    }

    private void DisposeAndNullifyClient()
    {
        _client?.Dispose();
        _client = null;
    }

    public void Dispose()
    {
        DisposeAndNullifyClient();

        _cfg.UnsubValueChanged(SunriseCCVars.VictoriaLogsEnabled, OnEnabledChanged);
        _cfg.UnsubValueChanged(SunriseCCVars.VictoriaLogsBaseAddress, OnBaseAddressChanged);
        _cfg.UnsubValueChanged(SunriseCCVars.VictoriaLogsStoreInDatabase, OnDatabaseStoreChanged);
        _cfg.UnsubValueChanged(CVars.WatchdogKey, OnWatchdogKeyChanged);
    }

    private void OnEnabledChanged(bool enabled)
    {
        _enabled = enabled;

        if (enabled && _client == null)
            _client = new();
        else if (!enabled && _client != null)
            DisposeAndNullifyClient();
    }

    private void OnBaseAddressChanged(string baseAddress)
    {
        _baseAddress = baseAddress;

        if (string.IsNullOrEmpty(_baseAddress))
            _sawmill.Error("Enabled VictoriaLogs shouldn't have empty BaseUrl CVar. Every sent request would fail instantly");
    }

    private void OnDatabaseStoreChanged(bool enabled)
    {
        _storeInDatabase = enabled;
    }

    private void OnWatchdogKeyChanged(string key)
    {
        _serverId = string.IsNullOrEmpty(key) ? "localhost" : key;
    }
}

