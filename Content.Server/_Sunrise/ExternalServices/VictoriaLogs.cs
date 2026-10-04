using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Content.Server.Database;
using Content.Shared._Sunrise.SunriseCCVars;
using Robust.Shared.Configuration;
using YamlDotNet.Serialization.NamingConventions;

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

    private const string AdditionalUriPart = "/insert/jsonline?";

    void IPostInjectInit.PostInject()
    {
        _sawmill = _log.GetSawmill("VictoriaLogs");

        _cfg.OnValueChanged(SunriseCCVars.VictoriaLogsEnabled, OnEnabledChanged, true);
        _cfg.OnValueChanged(SunriseCCVars.VictoriaLogsBaseAddress, OnBaseAddressChanged, true);
        _cfg.OnValueChanged(SunriseCCVars.VictoriaLogsStoreInDatabase, OnDatabaseStoreChanged, true);
    }

    public async void HandleDefaultAdminLog(AdminLog log)
    {
        if (!_enabled)
            return;


        var json = JsonSerializer.Serialize(new VictoriaAdminLog(log), _jsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        _sawmill.Info(json);

        var uriString = _baseAddress + AdditionalUriPart;
        var uriKind = new UriCreationOptions()
        if (!Uri.TryCreate(uriString, out var uri))

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
}

