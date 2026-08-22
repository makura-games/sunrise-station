// © SUNRISE, An EULA/CLA with a hosting restriction, full text: https://github.com/makura-games/sunrise-station/blob/master/CLA.txt

using Content.Server._Sunrise.PlayerCache;
using Content.Server._Sunrise.SponsorValidation;
using Content.Shared._Sunrise.Pets;
using Content.Shared._Sunrise.PlayerCache;

namespace Content.Server._Sunrise.Pets;

public sealed partial class PetSelectionSystem : EntitySystem
{
    [Dependency] private SponsorValidationSystem _validationSystem = default!;
    [Dependency] private PlayerCacheManager _playerCache = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<PetSelectionPrototypeSelectedEvent>(OnPetSelectionSelected);
    }

    private void OnPetSelectionSelected(PetSelectionPrototypeSelectedEvent ev, EntitySessionEventArgs args)
    {
        if (!_validationSystem.ValidatePetSelection(ev.SelectedPetSelection, args.SenderSession.UserId))
            return;
        if (_playerCache.TryGetCache(args.SenderSession.UserId, out var cache))
        {
            cache.Pet = ev.SelectedPetSelection;
            _playerCache.SetCache(args.SenderSession.UserId, cache);
            return;
        }
        cache = new PlayerCacheData();
        _playerCache.SetCache(args.SenderSession.UserId, cache);
    }
}
