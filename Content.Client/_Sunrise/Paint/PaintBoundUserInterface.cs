using Content.Client._Sunrise.Paint.UI;
using Content.Shared._Sunrise.Paint;
using Robust.Client.UserInterface;

namespace Content.Client._Sunrise.Paint;

public sealed class PaintBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private PaintColorWindow? _window;
    private Color? _lastReceivedColor;

    public override void Update()
    {
        if (_window == null || !EntMan.TryGetComponent(Owner, out PaintComponent? paint))
            return;

        if (_lastReceivedColor == paint.Color)
            return;

        _lastReceivedColor = paint.Color;
        _window.SetColor(paint.Color);
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<PaintColorWindow>();
        _window.OnApplyButtonPressed += color => SendPredictedMessage(new PaintSetColorMessage(color));
        _lastReceivedColor = null;
        Update();
    }
}
