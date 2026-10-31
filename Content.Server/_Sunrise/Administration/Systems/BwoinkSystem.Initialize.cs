using Content.Server.Administration.Systems;
using Content.Server.Administration.Managers;
using Content.Server._Sunrise.PlayerCache;
using Content.Server._Sunrise.SponsorSystem;
using Content.Shared._Sunrise.SponsorSystem;
using Content.Sunrise.Interfaces.Shared;
using Content.Shared.Administration;
using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Server.Administration.Systems;

public partial class BwoinkSystem
{
    [Dependency] private INetConfigurationManager _netConfig = default!;
    [Dependency] private PlayerCacheManager _playerCacheManager = default!;
    [Dependency] private IBanManager _banManager = default!;

    private ISharedSponsorsManager? _sponsorsManager;

    partial void InitializeSunrise()
    {
        IoCManager.Instance!.TryResolveType(out _sponsorsManager);
        SubscribeNetworkEvent<BwoinkRequestDbMessages>(OnRequestDbMessages);
    }
}
