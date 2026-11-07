using System.Numerics;
using System.Text;
using Content.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Client.Lobby.UI;

public sealed partial class LobbyCharacterPreviewPanel
{
    [Dependency] private IEntityManager _entManager = default!;

    public event Action? OnChangePetRequested;

    private EntityUid? _petPreviewDummy;
    private SpriteView? _petSpriteView;

    private void InitializeSunrisePetPreview()
    {
        Header.Visible = false;
        ChangePetButton.OnPressed += OnChangePetButtonPressed;
    }

    public void SetPetSprite(EntityUid? uid)
    {
        if (_petPreviewDummy is { } oldPet && (uid == null || oldPet != uid))
            _entManager.DeleteEntity(oldPet);

        if (_petSpriteView != null)
        {
            ViewBox.RemoveChild(_petSpriteView);
            _petSpriteView = null;
        }

        if (uid is not { } pet || !pet.IsValid())
        {
            _petPreviewDummy = null;
            return;
        }

        _petPreviewDummy = pet;
        _petSpriteView = new SpriteView
        {
            OverrideDirection = Direction.South,
            Scale = new Vector2(2f, 2f),
            MaxSize = new Vector2(80, 80),
            Stretch = SpriteView.StretchMode.Fill,
            VerticalAlignment = Control.VAlignment.Bottom,
        };
        _petSpriteView.SetEntity(pet);
        ViewBox.AddChild(_petSpriteView);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ChangePetButton.OnPressed -= OnChangePetButtonPressed;

            if (_petPreviewDummy is { } pet)
                _entManager.DeleteEntity(pet);

            _petPreviewDummy = null;
        }

        base.Dispose(disposing);
    }

    private void OnChangePetButtonPressed(BaseButton.ButtonEventArgs args)
    {
        OnChangePetRequested?.Invoke();
    }

    private string WrapSunriseSummaryText(string text, float maxWidth)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var font = Summary.FontOverride ?? UserInterfaceManager.ThemeDefaults.LabelFont;
        var uiScale = UIScale;
        var words = text.Split(' ');
        var lines = new List<string>();
        var currentLine = new StringBuilder();

        foreach (var word in words)
        {
            var testLine = currentLine.Length > 0 ? $"{currentLine} {word}" : word;
            var testWidth = MeasureSunriseTextWidth(testLine, font, uiScale);

            if (testWidth <= maxWidth * uiScale)
            {
                if (currentLine.Length > 0)
                    currentLine.Append(' ');

                currentLine.Append(word);
                continue;
            }

            if (currentLine.Length > 0)
            {
                lines.Add(currentLine.ToString());
                currentLine.Clear();
            }

            currentLine.Append(word);
        }

        if (currentLine.Length > 0)
            lines.Add(currentLine.ToString());

        return string.Join("\n", lines);
    }

    private static float MeasureSunriseTextWidth(string text, Font font, float uiScale)
    {
        var width = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            if (font.TryGetCharMetrics(rune, uiScale, out var metrics))
                width += metrics.Advance;
        }

        return width;
    }
}
