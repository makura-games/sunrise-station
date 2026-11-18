using Content.Client.Lobby;
using Content.Shared._Sunrise.SponsorInventory;
using Content.Shared.Preferences;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client._Sunrise.SponsorInventory;

/// <summary>
/// UI controller that owns the lobby sponsor inventory window lifecycle.
/// </summary>
public sealed partial class InventoryUIController : UIController, IOnStateEntered<LobbyState>, IOnStateExited<LobbyState>
{
    [Dependency] private IClientPreferencesManager _preferences = default!;

    [UISystemDependency] private readonly SunriseInventorySystem _sponsorInventory = default!;

    private InventoryWindow? _window;

    /// <summary>
    /// Opens the sponsor inventory window for the currently selected lobby character.
    /// </summary>
    public void Open()
    {
        _sponsorInventory.RequestInitialData();
        EnsureWindow();
        RefreshWindow();

        if (_window is { IsOpen: true })
            return;

        _window?.OpenCentered();
    }

    public void OnStateEntered(LobbyState state)
    {
        _sponsorInventory.RequestInitialData();
    }

    public void OnStateExited(LobbyState state)
    {
        CloseWindow();
    }

    private void EnsureWindow()
    {
        if (_window != null)
            return;

        _window = UIManager.CreateWindow<InventoryWindow>();
        _window.OnSaveRequested += SaveProfile;
        _window.OnPetSelected += SelectPet;
    }

    private void RefreshWindow()
    {
        if (_window == null || _preferences.Preferences == null)
            return;

        var selectedSlot = _preferences.Preferences.SelectedCharacterIndex;
        _window.SetProfile(
            _preferences.Preferences.SelectedCharacter as HumanoidCharacterProfile,
            selectedSlot,
            _sponsorInventory.GetInventoryProfile(selectedSlot));
    }

    private void SaveProfile(HumanoidCharacterProfile profile, int slot, SunriseInventoryProfile inventoryProfile)
    {
        _sponsorInventory.SetInventoryProfile(slot, inventoryProfile);
        _preferences.UpdateCharacter(profile, slot);
        UIManager.GetUIController<LobbyUIController>().ReloadCharacterSetup();
        RefreshWindow();
    }

    private void SelectPet(string? petSelection)
    {
        _sponsorInventory.SelectPet(petSelection);
        _window?.RefreshPets();
    }

    private void CloseWindow()
    {
        if (_window == null)
            return;

        _window.OnSaveRequested -= SaveProfile;
        _window.OnPetSelected -= SelectPet;
        _window.DetachExternalSubscriptions();
        _window.Close();
        _window = null;
    }
}
