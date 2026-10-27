using Robust.Shared.Network;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Client.UserInterface.Systems.Info;

public sealed partial class InfoUIController
{
    public bool HasRulesInformation { get; private set; }
    public bool IsRulesPopupOpen => _rulesPopup != null;
    public event Action? RulesInformationUpdated;
    public event Action? RulesPopupClosed;

    private void SunriseInitializeRulesQueue()
    {
        _netManager.Connected += OnSunriseConnected;
        _netManager.Disconnect += OnSunriseDisconnected;
    }

    private void SunriseOnRulesInformationReceived()
    {
        HasRulesInformation = true;
    }

    private void SunriseOnRulesInformationHandled()
    {
        RulesInformationUpdated?.Invoke();
    }

    private void SunriseOnRulesAccepted(bool hadPopup)
    {
        if (hadPopup)
            RulesPopupClosed?.Invoke();
    }

    private void OnSunriseConnected(object? sender, NetChannelArgs args)
    {
        HasRulesInformation = false;
    }

    private void OnSunriseDisconnected(object? sender, NetDisconnectedArgs args)
    {
        HasRulesInformation = false;
        _rulesPopup?.Orphan();
        _rulesPopup = null;
    }
}
