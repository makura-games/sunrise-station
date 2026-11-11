using Content.Client.UserInterface.Systems.Chat;
using Content.Shared._Sunrise.SunriseCCVars;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;

namespace Content.Client._Sunrise.ChatIcons;

public sealed partial class ChatIconsSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IUserInterfaceManager _uiMan = default!;

    public override void Initialize()
    {
        base.Initialize();

        _cfg.OnValueChanged(SunriseCCVars.ChatIconsEnabled, OnRadioIconsChanged, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _cfg.UnsubValueChanged(SunriseCCVars.ChatIconsEnabled, OnRadioIconsChanged);
    }

    private void OnRadioIconsChanged(bool enable)
    {
        _uiMan.GetUIController<ChatUIController>().Repopulate();
    }
}
