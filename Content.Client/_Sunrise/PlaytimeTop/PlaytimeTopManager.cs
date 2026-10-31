// © SUNRISE, An EULA/CLA with a hosting restriction, full text: https://github.com/space-sunrise/space-station-14/blob/master/CLA.txt

using Content.Shared._Sunrise.PlaytimeTop;
using Robust.Shared.Network;

namespace Content.Client._Sunrise.PlaytimeTop;

/// <summary>
/// Client manager for top players by playtime.
/// Receives data from the server and notifies UI of updates.
/// </summary>
public sealed partial class PlaytimeTopManager
{
    [Dependency] private IClientNetManager _netManager = default!;

    public List<PlaytimeTopEntry> OnlineNow { get; private set; } = [];
    public List<PlaytimeTopEntry> ActiveWeek { get; private set; } = [];
    public List<PlaytimeTopEntry> ActiveMonth { get; private set; } = [];
    public List<PlaytimeTopEntry> AllTime { get; private set; } = [];

    /// <summary>Invoked when updated top list data is received from the server.</summary>
    public event Action? PlaytimeTopChanged;

    public void Initialize()
    {
        _netManager.RegisterNetMessage<MsgPlaytimeTop>(OnPlaytimeTopReceived);
    }

    private void OnPlaytimeTopReceived(MsgPlaytimeTop msg)
    {
        OnlineNow = msg.OnlineNow;
        ActiveWeek = msg.ActiveWeek;
        ActiveMonth = msg.ActiveMonth;
        AllTime = msg.AllTime;
        PlaytimeTopChanged?.Invoke();
    }
}
