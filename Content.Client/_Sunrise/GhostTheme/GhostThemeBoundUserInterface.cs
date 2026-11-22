// © SUNRISE, An EULA/CLA with a hosting restriction, full text: https://github.com/makura-games/sunrise-station/blob/master/CLA.txt
using Content.Shared._Sunrise.GhostTheme;
using JetBrains.Annotations;

namespace Content.Client._Sunrise.GhostTheme;

[UsedImplicitly]
public sealed class GhostThemeBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private GhostThemeMenu? _menu;

    public GhostThemeBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = new GhostThemeMenu();
        _menu.OnClose += Close;
        _menu.OnIdSelected += OnIdSelected;
        _menu.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not GhostThemeBoundUserInterfaceState st)
            return;

        _menu?.UpdateState(st.GhostThemes);
    }

    private void OnIdSelected(string selectedId)
    {
        SendMessage(new GhostThemePrototypeSelectedMessage(selectedId));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _menu?.Close();
            _menu = null;
        }
    }
}
