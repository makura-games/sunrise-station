using Content.Client._Sunrise.Roadmap;
using Content.Client._Sunrise.Tutorial;
using Content.Client._Sunrise.Tutorial.TutorialWindow;
using Content.Client.FeedbackPopup;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Guidebook;
using Content.Client.UserInterface.Systems.Info;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared.CCVar;
using JetBrains.Annotations;
using Robust.Client.Console;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BaseButton;

namespace Content.Client.UserInterface.Systems.EscapeMenu;

[UsedImplicitly]
public sealed partial class EscapeUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>
{
    [Dependency] private IClientConsoleHost _console = default!;
    [Dependency] private IUriOpener _uri = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private ChangelogUIController _changelog = default!;
    [Dependency] private InfoUIController _info = default!;
    [Dependency] private OptionsUIController _options = default!;
    [Dependency] private GuidebookUIController _guidebook = default!;
    [Dependency] private FeedbackPopupUIController _feedback = null!;
    [Dependency] private IEntityManager _entity = default!; // Sunrise
    [Dependency] private IEntitySystemManager _entSys = default!; // Sunrise

    private Options.UI.EscapeMenu? _escapeWindow;

    private MenuButton? EscapeButton => UIManager.GetActiveUIWidgetOrNull<MenuBar.Widgets.GameTopMenuBar>()?.EscapeButton;

    public void UnloadButton()
    {
        if (EscapeButton == null)
        {
            return;
        }

        EscapeButton.Pressed = false;
        EscapeButton.OnPressed -= EscapeButtonOnOnPressed;
    }

    public void LoadButton()
    {
        if (EscapeButton == null)
        {
            return;
        }

        EscapeButton.OnPressed += EscapeButtonOnOnPressed;
    }

    private void ActivateButton() => EscapeButton!.SetClickPressed(true);
    private void DeactivateButton() => EscapeButton!.SetClickPressed(false);

    public void OnStateEntered(GameplayState state)
    {
        DebugTools.Assert(_escapeWindow == null);

        _escapeWindow = UIManager.CreateWindow<Options.UI.EscapeMenu>();

        _escapeWindow.OnClose += DeactivateButton;
        _escapeWindow.OnOpen += ActivateButton;

        _escapeWindow.FeedbackButton.OnPressed += _ =>
        {
            CloseEscapeWindow();
            _feedback.ToggleWindow();
        };

        _escapeWindow.ChangelogButton.OnPressed += _ =>
        {
            CloseEscapeWindow();
            _changelog.ToggleWindow();
        };

        // Sunrise-Start
        _escapeWindow.RoadmapButton.OnPressed += _ =>
        {
            CloseEscapeWindow();
            UIManager.GetUIController<RoadmapUIController>().ToggleRoadmap();
        };

        var tutorialUi = UIManager.GetUIController<TutorialUIController>();
        UpdateQuitTutorialButtonVisibility();

        _escapeWindow.QuitTutorialButton.OnPressed += _ =>
        {
            CloseEscapeWindow();
            var tutorialSystem = _entSys.GetEntitySystem<TutorialSystem>();
            tutorialSystem.RequestQuitTutorial();
        };
        // Sunrise-End

        _escapeWindow.RulesButton.OnPressed += _ =>
        {
            CloseEscapeWindow();
            _info.OpenWindow();
        };

        _escapeWindow.DisconnectButton.OnPressed += _ =>
        {
            CloseEscapeWindow();
            _console.ExecuteCommand("disconnect");
        };

        _escapeWindow.OptionsButton.OnPressed += _ =>
        {
            CloseEscapeWindow();
            _options.OpenWindow();
        };

        _escapeWindow.QuitButton.OnPressed += _ =>
        {
            CloseEscapeWindow();
            _console.ExecuteCommand("quit");
        };

        // Sunrise-Start
        _escapeWindow.DonateButton.OnPressed += _ =>
        {
            _uri.OpenUri(_cfg.GetCVar(SunriseCCVars.InfoLinksDonate));
        };

        _escapeWindow.ReplaysButton.OnPressed += _ =>
        {
            _uri.OpenUri(_cfg.GetCVar(SunriseCCVars.InfoLinksReplays));
        };
        // Sunrise-End

        _escapeWindow.WikiButton.OnPressed += _ =>
        {
            _uri.OpenUri(_cfg.GetCVar(CCVars.InfoLinksWiki));
        };

        _escapeWindow.GuidebookButton.OnPressed += _ =>
        {
            _guidebook.ToggleGuidebook();
        };

        // Hide wiki button if we don't have a link for it.
        _escapeWindow.WikiButton.Visible = _cfg.GetCVar(CCVars.InfoLinksWiki) != "";


        // Sunrise-Start
        _escapeWindow.DonateButton.Visible = _cfg.GetCVar(SunriseCCVars.InfoLinksDonate) != "";
        _escapeWindow.ReplaysButton.Visible = _cfg.GetCVar(SunriseCCVars.InfoLinksReplays) != "";
        // Sunrise-End

        CommandBinds.Builder
            .Bind(EngineKeyFunctions.EscapeMenu,
                InputCmdHandler.FromDelegate(_ => ToggleWindow()))
            .Register<EscapeUIController>();
    }

    public void OnStateExited(GameplayState state)
    {
        if (_escapeWindow != null)
        {
            _escapeWindow.Dispose();
            _escapeWindow = null;
        }

        CommandBinds.Unregister<EscapeUIController>();
    }

    private void EscapeButtonOnOnPressed(ButtonEventArgs obj)
    {
        ToggleWindow();
    }

    private void CloseEscapeWindow()
    {
        _escapeWindow?.Close();
    }

    // Sunrise-start
    private void UpdateQuitTutorialButtonVisibility()
    {
        if (_escapeWindow == null)
            return;

        var session = IoCManager.Resolve<IPlayerManager>().LocalSession;
        _escapeWindow.QuitTutorialButton.Visible =
            session != null && _entity.HasComponent<TutorialPlayerComponent>(session.AttachedEntity);
    }
    // Sunrise-end

    /// <summary>
    /// Toggles the game menu.
    /// </summary>
    public void ToggleWindow()
    {
        if (_escapeWindow == null)
            return;

        if (_escapeWindow.IsOpen)
        {
            CloseEscapeWindow();
            EscapeButton!.Pressed = false;
        }
        else
        {
            // Sunrise-start
            UpdateQuitTutorialButtonVisibility();
            // Sunrise-end
            _escapeWindow.OpenCentered();
            EscapeButton!.Pressed = true;
        }
    }
}
