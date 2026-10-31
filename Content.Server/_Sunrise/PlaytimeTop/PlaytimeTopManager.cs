using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared._Sunrise.PlaytimeTop;
using Robust.Server.Player;
using Robust.Shared.Asynchronous;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.PlaytimeTop;

/// <summary>
/// Server manager for top players by playtime.
/// Periodically updates data from the database and broadcasts it to clients.
/// </summary>
public sealed partial class PlaytimeTopManager
{
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private IServerNetManager _netMgr = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private ITaskManager _taskManager = default!;

    private ISawmill _sawmill = default!;

    private const int TopCount = 20;
    private readonly TimeSpan _updateRate = TimeSpan.FromMinutes(1);
    private TimeSpan _nextUpdate = TimeSpan.Zero;
    private bool _isUpdating;
    private bool _hasInitialLoad;

    private List<PlaytimeTopEntry> _cachedOnlineNow = [];
    private List<PlaytimeTopEntry> _cachedActiveWeek = [];
    private List<PlaytimeTopEntry> _cachedActiveMonth = [];
    private List<PlaytimeTopEntry> _cachedAllTime = [];

    public void Initialize()
    {
        _sawmill = _logManager.GetSawmill("playtime_top");
        _netMgr.RegisterNetMessage<MsgPlaytimeTop>();
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus == SessionStatus.Connected)
        {
            SendTo(e.Session);

            if (_nextUpdate <= _timing.CurTime && !_isUpdating)
            {
                _nextUpdate = _timing.CurTime + _updateRate;
                UpdateTopAsync();
            }
        }
    }

    public void Update()
    {
        if (_nextUpdate > _timing.CurTime || _isUpdating)
            return;

        if (_hasInitialLoad && _playerManager.PlayerCount == 0)
            return;

        _nextUpdate = _timing.CurTime + _updateRate;
        UpdateTopAsync();
    }

    private async void UpdateTopAsync()
    {
        if (_isUpdating)
            return;

        _isUpdating = true;
        try
        {
            var (onlineNow, activeWeek, activeMonth, allTime) = await RefreshCaches();

            _taskManager.RunOnMainThread(() =>
            {
                _cachedOnlineNow = onlineNow;
                _cachedActiveWeek = activeWeek;
                _cachedActiveMonth = activeMonth;
                _cachedAllTime = allTime;
                _hasInitialLoad = true;
                _isUpdating = false;
                SendToAll();
            });
        }
        catch (Exception e)
        {
            _isUpdating = false;
            _sawmill.Error($"Ошибка при обновлении топа игроков: {e}");
        }
    }

    private async Task<(List<PlaytimeTopEntry> onlineNow, List<PlaytimeTopEntry> activeWeek,
        List<PlaytimeTopEntry> activeMonth, List<PlaytimeTopEntry> allTime)> RefreshCaches()
    {
        var cancel = CancellationToken.None;
        var now = DateTime.UtcNow;

        var daySince = now - TimeSpan.FromDays(1);
        var dayData = await _db.GetTopPlayersActiveSinceWithSession(daySince, TopCount, cancel);
        var onlineNow = dayData
            .Select(x => new PlaytimeTopEntry(x.Username, x.Time))
            .ToList();

        var weekSince = now - TimeSpan.FromDays(7);
        var weekData = await _db.GetTopPlayersActiveSinceWithSession(weekSince, TopCount, cancel);
        var activeWeek = weekData
            .Select(x => new PlaytimeTopEntry(x.Username, x.Time))
            .ToList();

        var monthSince = now - TimeSpan.FromDays(30);
        var monthData = await _db.GetTopPlayersActiveSinceWithSession(monthSince, TopCount, cancel);
        var activeMonth = monthData
            .Select(x => new PlaytimeTopEntry(x.Username, x.Time))
            .ToList();

        var allTimeData = await _db.GetTopPlayersOverall(TopCount, cancel);
        var allTime = allTimeData
            .Select(x => new PlaytimeTopEntry(x.Username, x.Time))
            .ToList();

        return (onlineNow, activeWeek, activeMonth, allTime);
    }

    private void SendToAll()
    {
        var msg = BuildMessage();
        foreach (var session in _playerManager.Sessions)
        {
            _netMgr.ServerSendMessage(msg, session.Channel);
        }
    }

    private void SendTo(ICommonSession session)
    {
        var msg = BuildMessage();
        _netMgr.ServerSendMessage(msg, session.Channel);
    }

    private MsgPlaytimeTop BuildMessage()
    {
        return new MsgPlaytimeTop
        {
            OnlineNow = _cachedOnlineNow,
            ActiveWeek = _cachedActiveWeek,
            ActiveMonth = _cachedActiveMonth,
            AllTime = _cachedAllTime,
        };
    }
}
