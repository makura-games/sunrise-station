using Content.Client._Sunrise.UserInterface.Controls;
using Content.Shared.Humanoid.Markings;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Client.Humanoid;

public sealed partial class LayerMarkingItem
{
    private List<MarkingEffectSelectorSliders>? _sunriseEffectSliders;
    private bool _updatingSunriseEffectFromUi;

    public void UpdateSunriseColorSelectors(Marking marking)
    {
        if (_updatingSunriseEffectFromUi)
            return;

        if (!ColorsContainer.Visible)
            return;

        if (_sunriseEffectSliders is not { } sliders)
            return;

        for (var i = 0; i < sliders.Count && i < marking.MarkingColors.Count; i++)
        {
            sliders[i].SetEffect(marking.GetMarkingEffectOrDefault(i));
        }
    }

    public bool TryCreateSunriseColorSelectors(Marking marking)
    {
        if (_sunriseEffectSliders is not null)
        {
            UpdateSunriseColorSelectors(marking);
            return true;
        }

        _sunriseEffectSliders = [];

        for (var i = 0; i < _markingPrototype.Sprites.Count; i++)
        {
            var container = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                HorizontalExpand = true,
            };

            var selector = new MarkingEffectSelectorSliders(marking.GetMarkingEffectOrDefault(i))
            {
                HorizontalExpand = true,
            };

            var label = _markingPrototype.Sprites[i] switch
            {
                SpriteSpecifier.Rsi rsi => Loc.GetString($"marking-{_markingPrototype.ID}-{rsi.RsiState}"),
                SpriteSpecifier.Texture texture => Loc.GetString($"marking-{_markingPrototype.ID}-{texture.TexturePath.Filename}"),
                _ => throw new InvalidOperationException("SpriteSpecifier not of known type"),
            };

            container.AddChild(new Label { Text = label });
            container.AddChild(selector);
            ColorsContainer.AddChild(container);
            _sunriseEffectSliders.Add(selector);

            var colorIndex = i;
            selector.OnColorChanged += effect =>
            {
                _updatingSunriseEffectFromUi = true;
                try
                {
                    _markingsModel.TrySetMarkingEffect(_organ, _layer, _markingPrototype.ID, colorIndex, effect);
                }
                finally
                {
                    _updatingSunriseEffectFromUi = false;
                }
            };
        }

        return true;
    }
}
