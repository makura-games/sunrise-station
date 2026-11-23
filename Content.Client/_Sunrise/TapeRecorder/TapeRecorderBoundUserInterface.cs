using Content.Shared._Sunrise.TapeRecorder;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;

namespace Content.Client._Sunrise.TapeRecorder;

public sealed class TapeRecorderBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private TapeRecorderMenu? _menu;

    protected override void Open()
    {
        if (IsOpened)
            return;

        base.Open();

        _menu = this.CreateWindow<TapeRecorderMenu>();
        _menu.OnModePressed += OnModePressed;
        _menu.OnPrintPressed += OnPrintPressed;
        Update();
    }

    public override void Update()
    {
        base.Update();

        if (_menu == null || !EntMan.TryGetComponent<TapeRecorderComponent>(Owner, out var recorder))
            return;

        TapeCassetteComponent? cassette = null;
        MetaDataComponent? cassetteMeta = null;
        if (recorder.Cassette != null)
        {
            EntMan.TryGetComponent(recorder.Cassette.Value, out cassette);
            EntMan.TryGetComponent(recorder.Cassette.Value, out cassetteMeta);
        }

        _menu.UpdateState(recorder, cassette, cassetteMeta);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _menu != null)
        {
            _menu.OnModePressed -= OnModePressed;
            _menu.OnPrintPressed -= OnPrintPressed;
            _menu = null;
        }

        base.Dispose(disposing);
    }

    private void OnModePressed(TapeRecorderMode mode)
    {
        SendPredictedMessage(new TapeRecorderSetModeMessage(mode));
    }

    private void OnPrintPressed()
    {
        SendPredictedMessage(new TapeRecorderPrintMessage());
    }
}
