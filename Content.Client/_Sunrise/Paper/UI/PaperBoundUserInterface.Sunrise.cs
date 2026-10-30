using static Content.Shared.Paper.PaperComponent;

// ReSharper disable once CheckNamespace
namespace Content.Client.Paper.UI;

public sealed partial class PaperBoundUserInterface
{
    private void InitializeTemplateFieldSupport()
    {
        if (_window == null)
            return;

        _window.OnTemplateRequested += OnTemplateRequested;
    }

    private void OnTemplateRequested(PaperTemplateRequestType type, int index)
    {
        SendMessage(new PaperTemplateRequestMessage(type, index));
    }
}
