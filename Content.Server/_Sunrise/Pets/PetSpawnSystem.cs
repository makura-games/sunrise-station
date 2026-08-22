// © SUNRISE, An EULA/CLA with a hosting restriction, full text: https://github.com/makura-games/sunrise-station/blob/master/CLA.txt

using Content.Server._Sunrise.PlayerCache;
using Content.Server._Sunrise.SponsorValidation;
using Content.Shared._Sunrise.Pets;
using Content.Shared.GameTicking;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Pets;

public sealed partial class PetSpawnSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private SponsorValidationSystem _validationSystem = default!;
    [Dependency] private PlayerCacheManager _playerCache = default!;
    [Dependency] private SharedPettingSystem _pettingSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (!_playerCache.TryGetCachedPet(ev.Player.UserId, out var petSelection))
            return;

        if (string.IsNullOrEmpty(petSelection))
            return;

        if (!_validationSystem.ValidatePetSelection(petSelection, ev.Player.UserId))
            return;

        if (!_prototypeManager.TryIndex<PetSelectionPrototype>(petSelection, out var petSelectionPrototype))
            return;

        if (string.IsNullOrEmpty(petSelectionPrototype.PetEntity))
            return;

        var coordinates = Transform(ev.Mob).Coordinates;
        var spawnedPet = Spawn(petSelectionPrototype.PetEntity, coordinates);

        if (!TryComp<PettableOnInteractComponent>(spawnedPet, out var pet))
            return;

        if (!TryComp<PetOnInteractComponent>(ev.Mob, out var pettingComponent))
            return;

        var master = (ev.Mob, pettingComponent);
        var petEntity = (spawnedPet, pet);

        if (!_pettingSystem.TrySetMaster(petEntity, master))
            return;

        _pettingSystem.Pet(petEntity);
    }
}
