// © SUNRISE, An EULA/CLA with a hosting restriction, full text: https://github.com/makura-games/sunrise-station/blob/master/CLA.txt
using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.GhostTheme;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]

public sealed partial class GhostThemeComponent: Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    [DataField, AutoNetworkedField]
    public string? GhostTheme;
}

[Serializable, NetSerializable]
public enum GhostThemeUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class GhostThemeInfo
{
    public string Id { get; }
    public bool IsAvailable { get; }

    public GhostThemeInfo(string id, bool isAvailable)
    {
        Id = id;
        IsAvailable = isAvailable;
    }
}

[Serializable, NetSerializable]
public sealed class GhostThemeBoundUserInterfaceState(List<GhostThemeInfo> ghostThemes)
    : BoundUserInterfaceState
{
    public readonly List<GhostThemeInfo> GhostThemes = ghostThemes;
}

[Serializable, NetSerializable]
public sealed class GhostThemePrototypeSelectedMessage: BoundUserInterfaceMessage
{
    public string SelectedGhostTheme { get; }

    public GhostThemePrototypeSelectedMessage(string selectedGhostTheme)
    {
        SelectedGhostTheme = selectedGhostTheme;
    }
}

public sealed partial class GhostThemeActionEvent : InstantActionEvent
{

}
