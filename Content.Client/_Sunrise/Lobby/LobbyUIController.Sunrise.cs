using System.Linq;
using Content.Client._Sunrise.Humanoid;
using Content.Client._Sunrise.PlayerCache;
using Content.Client.Body;
using Content.Client.Inventory;
using Content.Client.Station;
using Content.Shared._Sunrise.Humanoid;
using Content.Shared.Clothing;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Content.Sunrise.Interfaces.Shared;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Client.Lobby;

public sealed partial class LobbyUIController
{
    [Dependency] private PlayerCacheManager _playerCache = default!;
    [UISystemDependency] private readonly VisualBodySystem _visualBody = default!;
    [UISystemDependency] private readonly HumanoidProfileSystem _humanoidProfile = default!;
    [UISystemDependency] private readonly SunriseHumanoidProfileSystem _sunriseProfile = default!;
    [UISystemDependency] private readonly SunriseHumanoidProfileVisualSystem _sunriseProfileVisual = default!;
    [UISystemDependency] private readonly StationSpawningSystem _spawn = default!;
    private ISharedSponsorsManager? _sponsorsManager;

    private void InitializeSunrisePreviewHelpers()
    {
        IoCManager.Instance!.TryResolveType(out _sponsorsManager);
    }

    /// <summary>
    /// Applies the highest priority job's clothes to the dummy.
    /// </summary>
    public void GiveDummyJobClothesLoadout(EntityUid dummy, JobPrototype? jobPrototype, HumanoidCharacterProfile profile)
    {
        var job = jobPrototype ?? GetPreferredJob(profile);
        GiveDummyJobClothes(dummy, profile, job);

        var sponsorPrototypes = _sponsorsManager?.GetClientPrototypes().ToArray() ?? [];
        var jobLoadoutId = LoadoutSystem.GetJobPrototype(job.ID);
        var effectiveJobLoadoutId = LoadoutSystem.GetEffectiveRolePrototype(jobLoadoutId, _prototypeManager);
        if (!_prototypeManager.HasIndex(effectiveJobLoadoutId))
            return;

        var loadout = profile.GetLoadoutOrDefault(
            jobLoadoutId,
            _playerManager.LocalSession,
            profile.Species,
            EntityManager,
            _prototypeManager,
            sponsorPrototypes);
        GiveDummyLoadout(dummy, loadout, true);
    }

    /// <summary>
    /// Gets the highest priority job for the profile.
    /// </summary>
    public JobPrototype GetPreferredJob(HumanoidCharacterProfile profile)
    {
        var highPriorityJob = profile.JobPriorities.FirstOrDefault(priority => priority.Value == JobPriority.High).Key;
        return _prototypeManager.Index<JobPrototype>(highPriorityJob.Id ?? SharedGameTicker.FallbackOverflowJob);
    }

    public void GiveDummyLoadout(EntityUid uid, RoleLoadout? roleLoadout, bool outerwear)
    {
        if (roleLoadout == null)
            return;

        var underwearSlots = new HashSet<string> { "bra", "pants", "socks" };

        foreach (var group in roleLoadout.SelectedLoadouts.Values)
        {
            foreach (var loadout in group)
            {
                if (!_prototypeManager.Resolve(loadout.Prototype, out var loadoutPrototype))
                    continue;

                var wear = outerwear || loadoutPrototype.Equipment.Keys.All(underwearSlots.Contains);
                if (wear)
                    _spawn.EquipStartingGear(uid, loadoutPrototype);
            }
        }
    }

    /// <summary>
    /// Applies the specified job's clothes to the dummy.
    /// </summary>
    public void GiveDummyJobClothes(EntityUid dummy, HumanoidCharacterProfile profile, JobPrototype job)
    {
        var inventory = EntityManager.System<ClientInventorySystem>();
        if (!inventory.TryGetSlots(dummy, out var slots))
            return;

        if (profile.Loadouts.TryGetValue(job.ID, out var jobLoadout))
        {
            foreach (var loadouts in jobLoadout.SelectedLoadouts.Values)
            {
                foreach (var loadout in loadouts)
                {
                    if (!_prototypeManager.Resolve(loadout.Prototype, out var loadoutPrototype))
                        continue;

                    foreach (var slot in slots)
                    {
                        var equipment = _prototypeManager.Resolve(loadoutPrototype.StartingGear, out var loadoutGear)
                            ? (IEquipmentLoadout) loadoutGear
                            : loadoutPrototype;
                        ReplaceDummyEquipment(dummy, slot.Name, equipment.GetGear(slot.Name), inventory);
                    }
                }
            }
        }

        if (!_prototypeManager.Resolve(job.StartingGear, out var gear))
            return;

        foreach (var slot in slots)
            ReplaceDummyEquipment(dummy, slot.Name, ((IEquipmentLoadout) gear).GetGear(slot.Name), inventory);
    }

    /// <summary>
    /// Loads the profile onto a dummy entity.
    /// </summary>
    public EntityUid LoadProfileEntity(HumanoidCharacterProfile? humanoid, JobPrototype? job, bool jobClothes)
    {
        EntProtoId? previewEntity = null;
        if (humanoid != null && jobClothes)
        {
            job ??= GetPreferredJob(humanoid);
            previewEntity = job.JobPreviewEntity ?? (EntProtoId?) job.JobEntity;
        }

        EntityUid dummy;
        if (previewEntity != null)
        {
            dummy = EntityManager.SpawnEntity(previewEntity, MapCoordinates.Nullspace);
            return dummy;
        }

        if (humanoid is not null)
        {
            var dummyPrototype = _prototypeManager.Index(humanoid.Species).DollPrototype;
            dummy = EntityManager.SpawnEntity(dummyPrototype, MapCoordinates.Nullspace);
            _visualBody.ApplyProfileTo(dummy, humanoid);
            _humanoidProfile.ApplyProfileTo(dummy, humanoid);
            _sunriseProfile.ApplyProfileTo(dummy, humanoid);
            _sunriseProfileVisual.Refresh(dummy);
        }
        else
        {
            var dummyPrototype = _prototypeManager.Index(HumanoidCharacterProfile.DefaultSpecies).DollPrototype;
            dummy = EntityManager.SpawnEntity(dummyPrototype, MapCoordinates.Nullspace);
        }

        if (humanoid == null || job == null || !jobClothes)
            return dummy;

        GiveDummyJobClothes(dummy, humanoid, job);

        var jobLoadoutId = LoadoutSystem.GetJobPrototype(job.ID);
        var effectiveJobLoadoutId = LoadoutSystem.GetEffectiveRolePrototype(jobLoadoutId, _prototypeManager);
        if (!_prototypeManager.HasIndex<RoleLoadoutPrototype>(effectiveJobLoadoutId))
            return dummy;

        var sponsorPrototypes = _sponsorsManager?.GetClientPrototypes().ToArray() ?? [];
        var loadout = humanoid.GetLoadoutOrDefault(
            jobLoadoutId,
            _playerManager.LocalSession,
            humanoid.Species,
            EntityManager,
            _prototypeManager,
            sponsorPrototypes);
        GiveDummyLoadout(dummy, loadout, jobClothes);
        return dummy;
    }

    private void ReplaceDummyEquipment(
        EntityUid dummy,
        string slot,
        string prototype,
        ClientInventorySystem inventory)
    {
        if (inventory.TryUnequip(dummy, slot, out var unequippedItem, silent: true, force: true, reparent: false))
            EntityManager.DeleteEntity(unequippedItem.Value);

        if (prototype == string.Empty)
            return;

        var item = EntityManager.SpawnEntity(prototype, MapCoordinates.Nullspace);
        inventory.TryEquip(dummy, item, slot, true, true);
    }
}
