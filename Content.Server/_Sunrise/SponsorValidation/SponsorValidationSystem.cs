// © SUNRISE, An EULA/CLA with a hosting restriction, full text: https://github.com/makura-games/sunrise-station/blob/master/CLA.txt
using Content.Shared._Sunrise.GhostTheme;
using Content.Shared._Sunrise.Pets;
using Robust.Shared.Prototypes;
using Content.Sunrise.Interfaces.Shared;
using Robust.Shared.Network;

namespace Content.Server._Sunrise.SponsorValidation;

public sealed partial class SponsorValidationSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    private ISharedSponsorsManager? _sponsorsManager;

    public override void Initialize()
    {
        base.Initialize();
        IoCManager.Instance!.TryResolveType(out _sponsorsManager);
    }

    #region Ghost Theme Validation

    public bool ValidateGhostThemeSelection(string ghostThemeId, NetUserId userId)
    {
        if (!_prototypeManager.TryIndex<GhostThemePrototype>(ghostThemeId, out var ghostThemePrototype))
            return false;

        if (!ghostThemePrototype.SponsorOnly)
            return true;

        if (_sponsorsManager == null)
            return false;
        if (!_sponsorsManager.TryGetGhostThemes(userId, out var allowedThemes))
            return false;
        return allowedThemes.Contains(ghostThemeId);
    }

    public List<GhostThemeInfo> GetGhostThemesForPlayer(NetUserId userId)
    {
        var ghostThemes = new List<GhostThemeInfo>();
        List<string>? allowedThemes = null;
        _sponsorsManager?.TryGetGhostThemes(userId, out allowedThemes);

        foreach (var ghostThemeProto in _prototypeManager.EnumeratePrototypes<GhostThemePrototype>())
        {
            var isAvailable = !ghostThemeProto.SponsorOnly || (allowedThemes != null && allowedThemes.Contains(ghostThemeProto.ID));
            ghostThemes.Add(new GhostThemeInfo(ghostThemeProto.ID, isAvailable));
        }

        return ghostThemes;
    }

    #endregion

    #region Pet Selection Validation

    public bool ValidatePetSelection(string petId, NetUserId userId)
    {
        if (!_prototypeManager.TryIndex<PetSelectionPrototype>(petId, out var petPrototype))
            return false;

        if (!petPrototype.SponsorOnly)
            return true;

        if (_sponsorsManager == null)
            return false;
        if (!_sponsorsManager.TryGetPets(userId, out var allowedPets))
            return false;
        return allowedPets.Contains(petId);
    }

    public List<PetSelectionInfo> GetPetsForPlayer(NetUserId userId)
    {
        var pets = new List<PetSelectionInfo>();
        List<string>? allowedPets = null;
        _sponsorsManager?.TryGetPets(userId, out allowedPets);

        foreach (var petProto in _prototypeManager.EnumeratePrototypes<PetSelectionPrototype>())
        {
            var isAvailable = !petProto.SponsorOnly || (allowedPets != null && allowedPets.Contains(petProto.ID));
            pets.Add(new PetSelectionInfo(petProto.ID, isAvailable));
        }

        return pets;
    }

    #endregion

    #region General Sponsor Validation

    public bool ValidateSponsorAccess(string prototypeId, NetUserId userId, string prototypeType)
    {
        switch (prototypeType.ToLowerInvariant())
        {
            case "ghosttheme":
                return ValidateGhostThemeSelection(prototypeId, userId);
            case "pet":
                return ValidatePetSelection(prototypeId, userId);
            default:
                return false;
        }
    }

    public bool IsSponsor(NetUserId userId)
    {
        return _sponsorsManager?.IsSponsor(userId) == true;
    }

    #endregion
}
