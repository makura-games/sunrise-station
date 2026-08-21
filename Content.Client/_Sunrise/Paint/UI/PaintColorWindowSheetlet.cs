using Content.Client.Stylesheets;
using Content.Client.Stylesheets.SheetletConfigs;
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.UserInterface;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Sunrise.Paint.UI;

[CommonSheetlet]
public sealed class PaintColorWindowSheetlet : Sheetlet<NanotrasenStylesheet>
{
    public override StyleRule[] GetRules(NanotrasenStylesheet sheet, object config)
    {
        ISliderConfig sliderConfig = sheet;
        var backgroundTexture = sheet.GetTextureOr(sliderConfig.SliderFillPath, NanotrasenStylesheet.TextureRoot);
        var markerTexture = sheet.GetTextureOr(sliderConfig.SliderGrabber, NanotrasenStylesheet.TextureRoot);

        return
        [
            E<SaturationValueSelector>()
                .Identifier(SaturationValueSelector.StyleIdentifierSelector)
                .Prop(SaturationValueSelector.StylePropertyBackgroundTexture, backgroundTexture)
                .Prop(SaturationValueSelector.StylePropertyMarkerTexture, markerTexture),
        ];
    }
}
