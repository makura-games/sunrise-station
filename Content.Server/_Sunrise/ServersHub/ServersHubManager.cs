using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared._Sunrise.ServersHub;
using Content.Shared._Sunrise.SunriseCCVars;
using JetBrains.Annotations;
using Robust.Server.Player;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.ServersHub;

public sealed partial class ServersHubManager
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IServerNetManager _netMgr = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly List<ServerHubEntry> _serverDataList = new();

    private List<string> _serversList = new();

    private bool _enable;

    private readonly HttpClient _httpClient = new();
    private ISawmill _sawmill = default!;

    public TimeSpan NextTick = TimeSpan.Zero;

    // Не уверен что надо так часто это делать.
    private readonly TimeSpan _updateRate = TimeSpan.FromSeconds(5);

    private static readonly Regex RestrictedNameRegex = ServerNameRegex();

    public void Initialize()
    {
        _sawmill = _logManager.GetSawmill("serversHub");

        _cfg.OnValueChanged(SunriseCCVars.ServersHubList, OnServerListChanged, true);
        _cfg.OnValueChanged(SunriseCCVars.ServersHubEnable, OnServersHubEnableChanged, true);

        _netMgr.RegisterNetMessage<MsgFullServerHubList>();
    }

    private void OnServersHubEnableChanged(bool enable)
    {
        _enable = enable;
    }

    private void OnServerListChanged(string serverList)
    {
        var urls = new List<string>();

        foreach (var serverUrl in serverList.Split(','))
        {
            try
            {
                var uri = new Uri(serverUrl);
                urls.Add(uri.AbsoluteUri);
            }
            catch (UriFormatException)
            {
            }
        }

        _serversList = urls;
    }

    public void Update()
    {
        if (NextTick > _timing.CurTime)
            return;

        NextTick += _updateRate;

        UpdateServerData();
    }

    private async void UpdateServerData()
    {
        if (!_enable)
            return;

        if (_serversList.Count == 0)
            return;

        _serverDataList.Clear();

        foreach (var serverUrl in _serversList)
        {
            var data = await RefreshServerData(serverUrl);
            if (data == null)
                continue;

            var thisServerUrl = _cfg.GetCVar(CVars.HubServerUrl);

            var canConnect = true;

            if (thisServerUrl != "")
                canConnect = NormalizeUrl(thisServerUrl) != NormalizeUrl(serverUrl);

            var serverName = RestrictedNameRegex.Replace(data.Short_Name ?? data.Name, string.Empty);
            _serverDataList.Add(new ServerHubEntry(
                serverName,
                data.Map ?? "",
                data.Preset ?? "",
                data.Players,
                data.Soft_Max_Players,
                $"ss14s://{NormalizeUrl(serverUrl)}",
                canConnect));
        }

        SendFullPlayerList(_playerManager.Sessions);
    }

    private string NormalizeUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            return $"{uri.Host}{uri.AbsolutePath.TrimEnd('/')}";
        }
        catch (UriFormatException)
        {
            _sawmill.Error($"Invalid URL format: {url}");
            return url;
        }
    }

    private async Task<ServerDataResponse?> RefreshServerData(string url, CancellationToken cancel = default)
    {
        try
        {
            using var resp = await _httpClient.GetAsync($"{url}/status", cancel);

            if (resp.StatusCode == HttpStatusCode.NotFound)
                return null;

            if (!resp.IsSuccessStatusCode)
            {
                _sawmill.Error("SS14 server returned bad response {StatusCode}!", resp.StatusCode);
                return null;
            }

            var responseData = await resp.Content.ReadFromJsonAsync<ServerDataResponse>(cancellationToken: cancel);

            return responseData;
        }
        catch (HttpRequestException e)
        {
            _sawmill.Error("Failed to send ping to watchdog:\n{0}", e);
        }

        return null;
    }

    // Я в душе не ебу почему без _ оно не хочет парсить некоторые строки с json, но кому не похуй?
    [UsedImplicitly]
    private sealed record ServerDataResponse(
        string Name,
        string? Map,
        int Round_Id,
        int Players,
        int Soft_Max_Players,
        bool Panic_Bunker,
        int Run_Level,
        string? Preset,
        string? Round_Start_Time,
        string? Short_Name);

    private void SendFullPlayerList(IEnumerable<ICommonSession> sessions)
    {
        var netMsg = new MsgFullServerHubList
        {
            ServersHubEntries = _serverDataList,
        };

        foreach (var session in sessions)
        {
            _netMgr.ServerSendMessage(netMsg, session.Channel);
        }
    }

    [GeneratedRegex(@"[^А-Яа-яA-Za-zёЁ0-9' \-\.\[\]—:|]")]
    private static partial Regex ServerNameRegex();
}
