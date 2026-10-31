using Content.Client.Stylesheets;
using Content.Client.Stylesheets.SheetletConfigs;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Sunrise.Stylesheets.Sheetlets;

[CommonSheetlet]
public sealed class SunrisePanelHoverSheetlet<T> : Sheetlet<T> where T : PalettedStylesheet, IButtonConfig
{
    public override StyleRule[] GetRules(T sheet, object config)
    {
        var boxLight = new StyleBoxFlat { BackgroundColor = sheet.SecondaryPalette.BackgroundLight };

        return new StyleRule[]
        {
            E<PanelContainer>().Class(StyleClass.PanelDark).Pseudo(ContainerButton.StylePseudoClassHover).Panel(boxLight),
        };
    }
}
