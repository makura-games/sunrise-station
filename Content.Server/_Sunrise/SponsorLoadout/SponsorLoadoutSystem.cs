using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Shared.GameTicking;
using Robust.Shared.Prototypes;
using Content.Sunrise.Interfaces.Shared;

namespace Content.Server._Sunrise.SponsorLoadout;

public sealed partial class SponsorLoadoutSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private StationSpawningSystem _spawn = default!;
    private ISharedSponsorsManager? _sponsorsManager;

    public override void Initialize()
    {
        IoCManager.Instance!.TryResolveType(out _sponsorsManager);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (_sponsorsManager == null)
            return;

        if (!_sponsorsManager.TryGetSpawnEquipment(ev.Player.UserId, out var spawnEquipment))
            return;
        if (!_prototypeManager.TryIndex<SponsorLoadoutPrototype>(spawnEquipment, out var loadout))
            return;
        var isWhitelisted = ev.JobId != null &&
                            loadout.WhitelistJobs != null &&
                            !loadout.WhitelistJobs.Contains(ev.JobId);
        var isBlacklisted = ev.JobId != null &&
                            loadout.BlacklistJobs != null &&
                            loadout.BlacklistJobs.Contains(ev.JobId);
        var isSpeciesRestricted = loadout.SpeciesRestrictions != null &&
                                  loadout.SpeciesRestrictions.Contains(ev.Profile.Species);

        if (isWhitelisted || isBlacklisted || isSpeciesRestricted)
            return;

        if (!_prototypeManager.TryIndex(loadout.Equipment, out var startingGear))
            return;

        _spawn.EquipStartingGear(ev.Mob, startingGear);
    }
}
