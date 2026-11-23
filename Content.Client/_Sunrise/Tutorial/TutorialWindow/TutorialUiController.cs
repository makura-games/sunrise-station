using Content.Client.Lobby;
using Content.Client.UserInterface.Systems.Info;
using Content.Client._Sunrise.Tutorial;
using Content.Shared._Sunrise.Tutorial.Prototypes;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Prototypes;

namespace Content.Client._Sunrise.Tutorial.TutorialWindow;

public sealed partial class TutorialUIController : UIController,
    IOnStateEntered<LobbyState>,
    IOnStateExited<LobbyState>,
    IOnSystemChanged<TutorialSystem>
{
    [Dependency] private InfoUIController _info = default!;

    private TutorialSystem? _tutorialSystem;
    private TutorialSystem? _windowDataSystem;
    private TutorialWindow? _window;
    private TutorialPromptPopup? _prompt;
    private Action<TutorialSequencePrototype>? _startTutorialHandler;
    private Action? _requestCompletedTutorialsHandler;
    private bool _inLobby;

    public override void Initialize()
    {
        base.Initialize();

        _info.RulesInformationUpdated += TryShowTutorialPrompt;
        _info.RulesPopupClosed += TryShowTutorialPrompt;
    }

    public void OnSystemLoaded(TutorialSystem system)
    {
        _tutorialSystem = system;
        TryShowTutorialPrompt();
    }

    public void OnSystemUnloaded(TutorialSystem system)
    {
        _window?.Close();
        CloseTutorialPrompt();
        UnsubscribeWindowData();

        if (_tutorialSystem == system)
            _tutorialSystem = null;
    }

    public void ToggleTutorial()
    {
        if (_window != null)
        {
            _window.Close();
            return;
        }

        var tutorialSystem = _tutorialSystem;
        if (tutorialSystem == null)
            return;

        _window = UIManager.CreateWindow<TutorialWindow>();

        if (tutorialSystem.CompletedTutorialsReceived)
            _window.SetCompletedTutorials(tutorialSystem.CompletedTutorials);

        SubscribeWindowData(tutorialSystem);

        _startTutorialHandler = proto =>
            tutorialSystem.RequestStartTutorial(new ProtoId<TutorialSequencePrototype>(proto.ID));
        _requestCompletedTutorialsHandler = tutorialSystem.RequestWindowData;

        _window.OnTutorialButtonPressed += _startTutorialHandler;
        _window.OnRequestCompletedTutorials += _requestCompletedTutorialsHandler;
        _window.OnClose += OnWindowClosed;

        _window.OpenCentered();
    }

    public void OnStateEntered(LobbyState state)
    {
        _inLobby = true;
        TryShowTutorialPrompt();
    }

    public void OnStateExited(LobbyState state)
    {
        _inLobby = false;
        _window?.Close();
        CloseTutorialPrompt();
        UnsubscribeWindowData();
    }

    private void TryShowTutorialPrompt()
    {
        var tutorialSystem = _tutorialSystem;
        if (!_inLobby ||
            tutorialSystem == null ||
            _prompt != null ||
            tutorialSystem.IsTutorialPromptSeen() ||
            !_info.HasRulesInformation ||
            _info.IsRulesPopupOpen)
        {
            return;
        }

        ShowTutorialPrompt(tutorialSystem.GetTutorialPromptSkipDelay());
    }

    private void ShowTutorialPrompt(float skipDelay)
    {
        if (_prompt != null)
            return;

        var prompt = new TutorialPromptPopup
        {
            Timer = skipDelay
        };

        _prompt = prompt;
        prompt.OnStartPressed += OnPromptStartPressed;
        prompt.OnSkipPressed += OnPromptSkipPressed;

        UIManager.WindowRoot.AddChild(prompt);
        LayoutContainer.SetAnchorPreset(prompt, LayoutContainer.LayoutPreset.Wide);
    }

    private void OnPromptStartPressed()
    {
        DismissTutorialPrompt();

        if (_window == null)
            ToggleTutorial();
    }

    private void OnPromptSkipPressed()
    {
        DismissTutorialPrompt();
    }

    private void DismissTutorialPrompt()
    {
        _tutorialSystem?.MarkTutorialPromptSeen();
        CloseTutorialPrompt();
    }

    private void CloseTutorialPrompt()
    {
        var prompt = _prompt;
        if (prompt == null)
            return;

        prompt.OnStartPressed -= OnPromptStartPressed;
        prompt.OnSkipPressed -= OnPromptSkipPressed;
        prompt.Orphan();
        prompt.DisposeAllChildren();
        _prompt = null;
    }

    private void OnWindowDataReceived()
    {
        var tutorialSystem = _windowDataSystem;
        if (tutorialSystem == null)
            return;

        if (tutorialSystem.CompletedTutorialsReceived)
            _window?.SetCompletedTutorials(tutorialSystem.CompletedTutorials);
    }

    private void OnWindowClosed()
    {
        var window = _window;
        if (window == null)
            return;

        if (_startTutorialHandler != null)
            window.OnTutorialButtonPressed -= _startTutorialHandler;

        if (_requestCompletedTutorialsHandler != null)
            window.OnRequestCompletedTutorials -= _requestCompletedTutorialsHandler;

        window.OnClose -= OnWindowClosed;

        _startTutorialHandler = null;
        _requestCompletedTutorialsHandler = null;
        _window = null;
    }

    private void SubscribeWindowData(TutorialSystem system)
    {
        if (_windowDataSystem == system)
            return;

        UnsubscribeWindowData();

        system.WindowDataReceived += OnWindowDataReceived;
        _windowDataSystem = system;
    }

    private void UnsubscribeWindowData()
    {
        if (_windowDataSystem == null)
            return;

        _windowDataSystem.WindowDataReceived -= OnWindowDataReceived;
        _windowDataSystem = null;
    }
}
