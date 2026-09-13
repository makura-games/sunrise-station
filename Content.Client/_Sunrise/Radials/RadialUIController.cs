using Content.Client._Sunrise.UserInterface.Radial;
using Content.Client.CombatMode;
using Content.Client.ContextMenu.UI;
using Content.Client.Gameplay;
using Content.Shared._Sunrise.Radials;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client._Sunrise.Radials;

public sealed partial class RadialUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IUserInterfaceManager _userInterfaceManager = default!;
    [Dependency] private IEntityManager _entityManager = default!;

    [UISystemDependency] private readonly CombatModeSystem _combatMode = default!;
    [UISystemDependency] private readonly RadialSystem _radialSystem = default!;

    public EntityUid CurrentTarget;
    public SortedSet<Radial> CurrentRadials = new();

    /// <summary>
    ///     Separate from <see cref="ContextMenuUIController.RootMenu"/>, since we can open a verb menu as a submenu
    ///     of an entity menu element. If that happens, we need to be aware and close it properly.
    /// </summary>
    public RadialContainer? OpenMenu;

    public void OnStateEntered(GameplayState state)
    {
        //_context.OnContextClosed += Close;
        _radialSystem.OnRadialsResponse += HandleVerbsResponse;
    }

    public void OnStateExited(GameplayState state)
    {
        //_context.OnContextClosed -= Close;
        if (_radialSystem != null)
        {
            _radialSystem.OnRadialsResponse -= HandleVerbsResponse;
        }

        Close();
    }

    /// <summary>
    ///     Open a verb menu and fill it with verbs applicable to the given target entity.
    /// </summary>
    /// <param name="target">Entity to get verbs on.</param>
    /// <param name="force">Used to force showing all verbs (mostly for admins).</param>
    public void OpenRadialMenu(EntityUid target, bool force = false)
    {
        if (_playerManager.LocalSession?.AttachedEntity is not { Valid: true } user ||
            _combatMode.IsInCombatMode(user))
            return;

        Close();

        CurrentTarget = target;
        CurrentRadials = _radialSystem.GetRadials(target, user, Radial.RadialTypes, force);
        OpenMenu = new RadialContainer();
        OpenMenu.NormalSize = 50;
        OpenMenu.FocusSize = 64;

        //Feat: Disable action text, while im not fixed it
        OpenMenu.IsAction = false;
    }

    private void FillRadial()
    {
        OpenMenu ??= new RadialContainer();

        OpenMenu.CloseButton.Controller.OnPressed += _ => Close();

        foreach (var radial in CurrentRadials)
        {
            var button = OpenMenu.AddButton(radial.Text, radial.Icon ?? null);
            button.Controller.OnPressed += _ => { ExecuteRadial(radial); };
        }

        OpenMenu.Open(_userInterfaceManager.MousePositionScaled.Position);
    }

    public void AddServerRadials(List<Radial> radials)
    {
        CurrentRadials.UnionWith(radials);
        FillRadial();
    }

    private void Close()
    {
        if (OpenMenu == null)
            return;

        OpenMenu.Close();
        OpenMenu = null;
    }

    private void HandleVerbsResponse(RadialsResponseEvent msg)
    {
        if (OpenMenu == null || CurrentTarget != _entityManager.GetEntity(msg.Entity))
            return;

        if (msg.Radials == null)
            return;

        AddServerRadials(msg.Radials);
    }

    private void ExecuteRadial(Radial radial)
    {
        _radialSystem.ExecuteRadial(CurrentTarget, radial);

        if (radial.CloseMenu ?? radial.CloseMenuDefault)
            Close(); //_context.Close();
    }
}
