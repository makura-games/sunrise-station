using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Sunrise.Sheetlets;

[CommonSheetlet]
public sealed class SunriseButtonSheetlet : Sheetlet<PalettedStylesheet>
{
    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        return
        [
            E<Button>()
                .Class(SunriseStyleClass.StyleClassNoStyle)
                .Box(new StyleBoxFlat
                {
                    BackgroundColor = Color.Transparent,
                    ContentMarginLeftOverride = 15,
                    ContentMarginRightOverride = 15,
                    ContentMarginTopOverride = 12,
                    ContentMarginBottomOverride = 12,
                }),
        ];
    }
}
