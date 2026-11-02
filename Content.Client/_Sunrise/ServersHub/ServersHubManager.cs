using Content.Shared._Sunrise.ServersHub;
using Robust.Client.UserInterface;
using Robust.Shared.Network;

namespace Content.Client._Sunrise.ServersHub;

public sealed partial class ServersHubManager
{
    [Dependency] private IClientNetManager _netManager = default!;
    [Dependency] private IUserInterfaceManager _uiManager = default!;

    public event Action<List<ServerHubEntry>>? ServersDataListChanged;

    public List<ServerHubEntry> ServersDataList = [];

    private ServersHubUi _serversHubUi = default!;

    public void Initialize()
    {
        _netManager.RegisterNetMessage<MsgFullServerHubList>(OnServersDataChanged);
    }

    public void OpenWindow()
    {
        EnsureWindow();

        _serversHubUi.OpenCentered();
        _serversHubUi.MoveToFront();
    }

    private void EnsureWindow()
    {
        if (_serversHubUi is { Disposed: false })
            return;

        _serversHubUi = _uiManager.CreateWindow<ServersHubUi>();
    }

    public void ToggleWindow()
    {
        EnsureWindow();

        if (_serversHubUi.IsOpen)
        {
            _serversHubUi.Close();
        }
        else
        {
            OpenWindow();
        }
    }

    private void OnServersDataChanged(MsgFullServerHubList msg)
    {
        // Храним всю инфу внутри данного менеджера чтобы даже если конект с сервером будет разорван у нас работал хаб,
        // и его можно было отобразить после отключения или бана.
        ServersDataList = msg.ServersHubEntries;
        ServersDataListChanged?.Invoke(ServersDataList);
    }
}
