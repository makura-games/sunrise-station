using Content.Client._Sunrise.SponsorInventory;
using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Sunrise.Sheetlets;

/// <summary>
/// Style rules for the sponsor inventory lobby window.
/// </summary>
[CommonSheetlet]
public sealed class SponsorInventorySheetlet<T> : Sheetlet<T> where T : PalettedStylesheet
{
    private static readonly Color InventoryText = Color.FromHex("#D9DBE5");
    private static readonly Color InventoryTextMuted = Color.FromHex("#AEB2C5");
    private static readonly Color InventoryTextDisabled = Color.FromHex("#747783");

    public override StyleRule[] GetRules(T sheet, object config)
    {
        var panelBackground = sheet.SecondaryPalette.BackgroundLight;
        var insetBackground = sheet.SecondaryPalette.Background;
        var paletteBackground = sheet.SecondaryPalette.BackgroundLight;
        var borderSoft = sheet.SecondaryPalette.TextDark.WithAlpha(0.65f);

        return
        [
            E<PanelContainer>()
                .Class(SunriseStyleClass.SponsorInventoryContentPanel)
                .Panel(MakePanel(panelBackground, null, 8)),

            E<PanelContainer>()
                .Class(SunriseStyleClass.SponsorInventoryBagPanel)
                .Panel(MakePanel(insetBackground, null, 8)),

            E<PanelContainer>()
                .Class(SunriseStyleClass.SponsorInventoryPalettePanel)
                .Panel(MakePanel(insetBackground, null, 0)),

            E<PanelContainer>()
                .Class(SunriseStyleClass.SponsorInventoryPaletteListPanel)
                .Panel(MakePanel(paletteBackground, borderSoft, 0)),

            E<InventoryPaletteItemControl>()
                .Class(SunriseStyleClass.SponsorInventoryPaletteItem)
                .ParentOf(E())
                .ParentOf(E<Label>())
                .FontColor(InventoryTextMuted),

            E<InventoryPaletteItemControl>()
                .Class(SunriseStyleClass.SponsorInventoryPaletteItemSelected)
                .ParentOf(E())
                .ParentOf(E<Label>())
                .FontColor(InventoryText),

            E<InventoryPaletteItemControl>()
                .Class(SunriseStyleClass.SponsorInventoryPaletteItemUnavailable)
                .ParentOf(E())
                .ParentOf(E<Label>())
                .FontColor(InventoryTextDisabled),
        ];
    }

    private static StyleBoxFlat MakePanel(Color background, Color? border, float margin, int borderThickness = 1)
    {
        var styleBox = new StyleBoxFlat
        {
            BackgroundColor = background,
            ContentMarginLeftOverride = margin,
            ContentMarginRightOverride = margin,
            ContentMarginTopOverride = margin,
            ContentMarginBottomOverride = margin,
        };

        if (border != null)
        {
            styleBox.BorderColor = border.Value;
            styleBox.BorderThickness = new Thickness(borderThickness);
        }

        return styleBox;
    }
}
