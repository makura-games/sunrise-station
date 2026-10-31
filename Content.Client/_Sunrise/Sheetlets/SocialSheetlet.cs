using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Fonts;
using Content.Client.Stylesheets.Palette;
using Content.Client.Stylesheets.SheetletConfigs;
using Content.Client.Stylesheets.Sheetlets;
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Sunrise.Sheetlets;

[CommonSheetlet]
public sealed class SocialSheetlet<T> : Sheetlet<T>
    where T : PalettedStylesheet, IButtonConfig, IIconConfig
{
    private static readonly ColorPalette ForumPalette = ColorPalette.FromHexBase("#f1c40f");
    private static readonly ColorPalette DiscordPalette = ColorPalette.FromHexBase("#5865f2");
    private static readonly ColorPalette TelegramPalette = ColorPalette.FromHexBase("#0088cc");

    public override StyleRule[] GetRules(T sheet, object config)
    {
        var socialBox = new StyleBoxFlat
        {
            BackgroundColor = sheet.SecondaryPalette.BackgroundDark,
            BorderColor = sheet.SecondaryPalette.BackgroundLight,
            BorderThickness = new(2),
        };
        socialBox.SetContentMarginOverride(StyleBox.Margin.Horizontal, 10);
        socialBox.SetContentMarginOverride(StyleBox.Margin.Vertical, 5);

        var socialButton = new StyleBoxFlat(Color.White);

        var rules = new List<StyleRule>
        {
            E<PanelContainer>()
                .Class(SunriseStyleClass.StyleClassSocialBox)
                .Panel(socialBox),
            E<Button>()
                .Class(SunriseStyleClass.StyleClassSocialButton)
                .Box(socialButton),
            E<Button>()
                .Class(SunriseStyleClass.StyleClassSocialButton)
                .ParentOf(E<Label>())
                .Font(sheet.BaseFont.GetFont(16, FontKind.Bold)),
        };

        ButtonSheetlet<T>.MakeButtonRules<Button>(rules, ForumPalette, SunriseStyleClass.StyleClassSocialButtonForum);
        ButtonSheetlet<T>.MakeButtonRules<Button>(rules, DiscordPalette, SunriseStyleClass.StyleClassSocialButtonDiscord);
        ButtonSheetlet<T>.MakeButtonRules<Button>(rules, TelegramPalette, SunriseStyleClass.StyleClassSocialButtonTelegram);

        return rules.ToArray();
    }
}
