using Content.Client._Sunrise.Paint.UI;
using Content.Shared._Sunrise.Paint;
using Robust.Client.UserInterface;

namespace Content.Client._Sunrise.Paint;

public sealed class PaintBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private PaintColorWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<PaintColorWindow>();
        _window.OnApplyButtonPressed += OnApplyButtonPressed;
        Update();
    }

    public override void Update()
    {
        base.Update();

        if (_window == null || !EntMan.TryGetComponent(Owner, out PaintComponent? paint))
            return;

        _window.Color = paint.Color;
    }

    private void OnApplyButtonPressed(Color color)
    {
        SendMessage(new PaintSetColorMessage(color));
    }
}
