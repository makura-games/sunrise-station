using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Content.Shared._Sunrise.Contributors;
using Content.Shared._Sunrise.SunriseCCVars;
using Robust.Server.Player;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Contributors;

public sealed partial class ContributorsManager
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IServerNetManager _netMgr = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly List<ContributorEntry> _contributorsList = new();
    private bool _enable;
    private string _apiUrl = string.Empty;
    private string _projectName = string.Empty;

    private readonly HttpClient _httpClient = new();
    private ISawmill _sawmill = default!;

    private readonly TimeSpan _updateRate = TimeSpan.FromHours(1);
    private TimeSpan _nextUpdate = TimeSpan.Zero;

    public void Initialize()
    {
        _sawmill = _logManager.GetSawmill("contributors");

        _cfg.OnValueChanged(SunriseCCVars.ContributorsApiUrl, OnApiUrlChanged, true);
        _cfg.OnValueChanged(SunriseCCVars.ContributorsProjectName, OnProjectNameChanged, true);
        _cfg.OnValueChanged(SunriseCCVars.ContributorsApiToken, OnApiTokenChanged, true);
        _cfg.OnValueChanged(SunriseCCVars.ContributorsEnable, OnContributorsEnableChanged, true);

        _netMgr.RegisterNetMessage<MsgFullContributorsList>();
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus != SessionStatus.Connected || !_enable || _contributorsList.Count == 0)
            return;

        var netMsg = new MsgFullContributorsList
        {
            ContributorsEntries = _contributorsList,
        };
        _netMgr.ServerSendMessage(netMsg, e.Session.Channel);
    }

    private void OnContributorsEnableChanged(bool enable)
    {
        _enable = enable;

        if (enable && _contributorsList.Count == 0)
        {
            UpdateContributorsData();
        }
    }

    private void OnApiUrlChanged(string apiUrl)
    {
        _apiUrl = apiUrl;
    }

    private void OnProjectNameChanged(string projectName)
    {
        _projectName = projectName;
    }

    private void OnApiTokenChanged(string apiToken)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
    }

    public void Update()
    {
        if (_nextUpdate > _timing.CurTime)
            return;

        _nextUpdate += _updateRate;
        UpdateContributorsData();
    }

    private async void UpdateContributorsData()
    {
        if (!_enable)
            return;

        if (string.IsNullOrEmpty(_apiUrl) || string.IsNullOrEmpty(_projectName))
        {
            _sawmill.Warning("API URL or Project Name not set, skipping update");
            return;
        }

        if (string.IsNullOrEmpty(_cfg.GetCVar(SunriseCCVars.ContributorsApiToken)))
        {
            _sawmill.Error("API token is not set!");
            return;
        }

        var data = await RefreshContributorsData();
        if (data == null)
        {
            _sawmill.Warning("Failed to get contributors data");
            return;
        }

        _contributorsList.Clear();
        _contributorsList.AddRange(data);

        SendFullContributorsList(_playerManager.Sessions);
    }

    private async Task<List<ContributorEntry>?> RefreshContributorsData()
    {
        try
        {
            var requestUrl = $"{_apiUrl}/contributors/{_projectName}";
            _sawmill.Debug($"Sending request to: {requestUrl}");

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            using var resp = await _httpClient.SendAsync(request);

            if (resp.StatusCode == HttpStatusCode.NotFound)
            {
                _sawmill.Error("API endpoint not found");
                return null;
            }

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                _sawmill.Error("Invalid API token!");
                return null;
            }

            if (!resp.IsSuccessStatusCode)
            {
                _sawmill.Error($"API returned bad response {resp.StatusCode}!");
                return null;
            }

            var responseData = await resp.Content.ReadFromJsonAsync<ContributorsResponse>();
            if (responseData?.Contributors == null)
            {
                _sawmill.Error("Invalid response format from API");
                return null;
            }

            return responseData.Contributors.Select(c => new ContributorEntry(
                c.GithubId,
                c.GithubLogin,
                c.SS14UserId,
                c.SS14Username,
                c.Contributions)).ToList();
        }
        catch (HttpRequestException e)
        {
            _sawmill.Error($"Failed to get contributors data:\n{e}");
        }

        return null;
    }

    private void SendFullContributorsList(IEnumerable<ICommonSession> sessions)
    {
        var netMsg = new MsgFullContributorsList
        {
            ContributorsEntries = _contributorsList,
        };

        foreach (var session in sessions)
        {
            _sawmill.Debug($"Sending contributors data to player: {session.Name}");
            _netMgr.ServerSendMessage(netMsg, session.Channel);
        }
    }

    private sealed class ContributorsResponse
    {
        public List<ContributorInfo> Contributors { get; set; } = new();
    }

    private sealed class ContributorInfo
    {
        public int GithubId { get; set; }
        public string GithubLogin { get; set; } = string.Empty;
        public string SS14UserId { get; set; } = string.Empty;
        public string SS14Username { get; set; } = string.Empty;
        public int Contributions { get; set; }
    }
}
