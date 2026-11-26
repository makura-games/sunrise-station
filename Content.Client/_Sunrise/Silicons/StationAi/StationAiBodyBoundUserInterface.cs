using Content.Shared._Sunrise.Silicons.StationAi;
using Robust.Client.UserInterface;

namespace Content.Client._Sunrise.Silicons.StationAi;

public sealed class StationAiBodyBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private StationAiBodyWindow? _window;

    /// <summary>
    /// Creates the station AI body selection window and wires its commands to BUI messages.
    /// </summary>
    protected override void Open()
    {
        base.Open();

        _window = this.CreateDisposableControl<StationAiBodyWindow>();
        _window.OnClose += OnWindowClosed;
        _window.EnterBodyAction += EnterBody;
        _window.ExitBodyAction += ExitBody;
        _window.OpenCentered();

        Update();
    }

    public override void Update()
    {
        if (_window == null)
            return;

        if (!EntMan.TryGetComponent<StationAiBodyControllerComponent>(Owner, out var controller))
            return;

        _window.UpdateState(controller);
    }

    /// <summary>
    /// Detaches window event handlers when the body selection UI is disposed.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (!disposing || _window == null)
        {
            base.Dispose(disposing);
            return;
        }

        _window.OnClose -= OnWindowClosed;
        _window.EnterBodyAction -= EnterBody;
        _window.ExitBodyAction -= ExitBody;

        base.Dispose(disposing);
    }

    /// <summary>
    /// Closes the BUI only when the player closes this window directly.
    /// </summary>
    private void OnWindowClosed()
    {
        Close();
    }

    /// <summary>
    /// Sends a request to enter the selected AI body.
    /// </summary>
    private void EnterBody(NetEntity body)
    {
        SendPredictedMessage(new StationAiBodyEnterMessage(body));
    }

    /// <summary>
    /// Sends a request to return from the controlled body to the AI core.
    /// </summary>
    private void ExitBody()
    {
        SendPredictedMessage(new StationAiBodyExitMessage());
    }
}
