using System.Numerics;
using Content.Client._Sunrise.Sheetlets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.Stylesheets.Sheetlets;

[CommonSheetlet]
public sealed class SunriseDividersSheetlet : Sheetlet<PalettedStylesheet>
{
    private Color _backgroundColor = Color.FromHex("#404040");

    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var boxLowDivider = new StyleBoxFlat
        {
            BackgroundColor = _backgroundColor,
        };

        return
        [
            E<PanelContainer>()
                .Class(SunriseStyleClass.TutorialDivider)
                .Panel(boxLowDivider)
                .MinSize(new Vector2(2, 2)),
        ];
    }
}
