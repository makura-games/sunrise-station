using Content.Shared._Sunrise.Contributors;
using Robust.Shared.Network;

namespace Content.Client._Sunrise.Contributors;

public sealed partial class ContributorsManager
{
    [Dependency] private IClientNetManager _netManager = default!;

    public event Action<List<ContributorEntry>>? ContributorsDataListChanged;

    public List<ContributorEntry> ContributorsDataList = [];

    public ContributorsManager()
    {
        IoCManager.InjectDependencies(this);
    }

    public void Initialize()
    {
        _netManager.RegisterNetMessage<MsgFullContributorsList>(OnContributorsDataChanged);
    }

    private void OnContributorsDataChanged(MsgFullContributorsList msg)
    {
        ContributorsDataList = msg.ContributorsEntries;
        ContributorsDataListChanged?.Invoke(ContributorsDataList);
    }
}
