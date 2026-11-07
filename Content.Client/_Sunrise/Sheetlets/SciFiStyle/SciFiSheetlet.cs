using System.Numerics;
using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Colorspace;
using Content.Client.Stylesheets.Fonts;
using Content.Client.Stylesheets.SheetletConfigs;
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Sunrise.Sheetlets.SciFiStyle;

[CommonSheetlet]
public sealed class DonationTerminalSheetlet : Sheetlet<NanotrasenStylesheet>
{
    private static readonly ResPath TextureRoot = new("/Textures/_Sunrise/Interface/SciFi");
    private static readonly ResPath FrameTexturePath = new("sci_fi_frame_inset_gradient.svg.96dpi.png");
    private static readonly ResPath BackgroundTexturePath = new("sci_fi_frame_background.svg.96dpi.png");
    private static readonly ResPath PillButtonTexturePath = new("sci_fi_pill_button.svg.96dpi.png");

    public override StyleRule[] GetRules(NanotrasenStylesheet sheet, object config)
    {
        var buttonCfg = (IButtonConfig) sheet;
        var sciFiButtonTexture = sheet.GetTextureOr(buttonCfg.RoundedButtonPath, NanotrasenStylesheet.TextureRoot);
        var sciFiPillButtonTexture = sheet.GetTextureOr(PillButtonTexturePath, TextureRoot);
        var sciFiDonateButtonTexture = sheet.GetTextureOr(buttonCfg.RoundedButtonBorderedPath, NanotrasenStylesheet.TextureRoot);

        var background = sheet.GetTextureOr(BackgroundTexturePath, TextureRoot)
            .IntoPatch(StyleBox.Margin.All, SunriseStyleClass.SciFiFramePatchMargin);

        var frame = sheet.GetTextureOr(FrameTexturePath, TextureRoot)
            .IntoPatch(StyleBox.Margin.All, SunriseStyleClass.SciFiFramePatchMargin);
        frame.Modulate = SciFiPalette.Accent;

        var boxHighDivider = new StyleBoxFlat
        {
            BackgroundColor = SciFiPalette.BorderDim,
            ContentMarginBottomOverride = 2,
            ContentMarginLeftOverride = 2,
        };

        var boxSciFiDivider = new StyleBoxFlat(SciFiPalette.BorderDim);

        return
        [
            E<PanelContainer>()
                .Class(SunriseStyleClass.StyleClassSciFiFrameBackground)
                .Panel(background),

            E<PanelContainer>()
                .Class(SunriseStyleClass.StyleClassSciFiFrame)
                .Panel(frame),

            E<LineEdit>()
                .Class(SunriseStyleClass.StyleClassSciFiLineEdit)
                .Prop(LineEdit.StylePropertyStyleBox, MakeLineEdit())
                .Prop("font-color", SciFiPalette.Text)
                .Prop(LineEdit.StylePropertyCursorColor, SciFiPalette.Accent)
                .Prop(LineEdit.StylePropertySelectionColor, SciFiPalette.AccentSoft.WithAlpha(0.65f)),

            E<LineEdit>()
                .Class(SunriseStyleClass.StyleClassSciFiLineEdit)
                .Class(LineEdit.StyleClassLineEditNotEditable)
                .Prop("font-color", SciFiPalette.TextMuted.WithAlpha(0.65f)),

            E<LineEdit>()
                .Class(SunriseStyleClass.StyleClassSciFiLineEdit)
                .Pseudo(LineEdit.StylePseudoClassPlaceholder)
                .Prop("font-color", SciFiPalette.TextMuted.WithAlpha(0.82f)),

            E<PanelContainer>()
                .Class(SunriseStyleClass.StyleClassSciFiTabBar)
                .Panel(MakeTabBar()),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiButton)
                .PseudoNormal()
                .Modulate(Color.White)
                .Box(MakeButton(sciFiButtonTexture)),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiButton)
                .PseudoHovered()
                .Modulate(Color.White)
                .Box(MakeButton(sciFiButtonTexture, hovered: true)),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiButton)
                .PseudoPressed()
                .Modulate(Color.White)
                .Box(MakeButton(sciFiButtonTexture, pressed: true)),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiButton)
                .PseudoDisabled()
                .Modulate(Color.White)
                .Box(MakeButton(sciFiButtonTexture, disabled: true)),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiPillButton)
                .PseudoNormal()
                .Modulate(Color.White)
                .Box(MakePillButton(sciFiPillButtonTexture)),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiPillButton)
                .PseudoHovered()
                .Modulate(Color.White)
                .Box(MakePillButton(sciFiPillButtonTexture, hovered: true)),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiPillButton)
                .PseudoPressed()
                .Modulate(Color.White)
                .Box(MakePillButton(sciFiPillButtonTexture, pressed: true)),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiPillButton)
                .PseudoDisabled()
                .Modulate(Color.White)
                .Box(MakePillButton(sciFiPillButtonTexture, disabled: true)),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiButton)
                .ParentOf(E<Label>())
                .Font(sheet.BaseFont.GetFont(14, FontKind.Bold))
                .FontColor(SciFiPalette.Text),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiButton)
                .PseudoPressed()
                .ParentOf(E<Label>())
                .FontColor(SciFiPalette.TextMuted),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiButton)
                .PseudoDisabled()
                .ParentOf(E<Label>())
                .FontColor(SciFiPalette.TextMuted.WithAlpha(0.65f)),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiDonateButton)
                .PseudoNormal()
                .Modulate(Color.White)
                .Box(MakeDonateButton(sciFiDonateButtonTexture)),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiDonateButton)
                .PseudoHovered()
                .Modulate(Color.White)
                .Box(MakeDonateButton(sciFiDonateButtonTexture, hovered: true)),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiDonateButton)
                .PseudoPressed()
                .Modulate(Color.White)
                .Box(MakeDonateButton(sciFiDonateButtonTexture, pressed: true)),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiDonateButton)
                .PseudoDisabled()
                .Modulate(Color.White)
                .Box(MakeDonateButton(sciFiDonateButtonTexture, disabled: true)),

            E<Label>()
                .Class(SunriseStyleClass.StyleClassSciFiButtonLabel)
                .Font(sheet.BaseFont.GetFont(13, FontKind.Bold))
                .FontColor(SciFiPalette.Text),

            E<TextureRect>()
                .Class(SunriseStyleClass.StyleClassSciFiButtonIcon)
                .Modulate(SciFiPalette.Accent),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiIconButton)
                .PseudoNormal()
                .Modulate(Color.White)
                .Box(MakeIconButton()),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiIconButton)
                .PseudoHovered()
                .Modulate(Color.White)
                .Box(MakeIconButton(hovered: true)),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiIconButton)
                .PseudoPressed()
                .Modulate(Color.White)
                .Box(MakeIconButton(pressed: true)),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiIconButton)
                .PseudoDisabled()
                .Modulate(Color.White)
                .Box(MakeIconButton(disabled: true)),

            E<TextureRect>()
                .Class(SunriseStyleClass.StyleClassSciFiIconButtonIcon)
                .Modulate(SciFiPalette.Accent),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiIconButton)
                .PseudoHovered()
                .ParentOf(E<TextureRect>().Class(SunriseStyleClass.StyleClassSciFiIconButtonIcon))
                .Modulate(SciFiPalette.Text),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiIconButton)
                .PseudoPressed()
                .ParentOf(E<TextureRect>().Class(SunriseStyleClass.StyleClassSciFiIconButtonIcon))
                .Modulate(SciFiPalette.TextMuted),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiIconButton)
                .PseudoDisabled()
                .ParentOf(E<TextureRect>().Class(SunriseStyleClass.StyleClassSciFiIconButtonIcon))
                .Modulate(SciFiPalette.AccentDim.WithAlpha(0.9f)),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiTab)
                .PseudoNormal()
                .Box(MakeTab(active: false)),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiTab)
                .PseudoHovered()
                .Box(MakeTab(active: false, hovered: true)),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiTab)
                .PseudoPressed()
                .Box(MakeTab(active: false, pressed: true)),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiTab)
                .PseudoDisabled()
                .Box(MakeTab(active: false, disabled: true)),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiTab)
                .Class(SunriseStyleClass.StyleClassSciFiTabActive)
                .PseudoNormal()
                .Box(MakeTab(active: true)),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiTab)
                .Class(SunriseStyleClass.StyleClassSciFiTabActive)
                .PseudoHovered()
                .Box(MakeTab(active: true, hovered: true)),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiTab)
                .Class(SunriseStyleClass.StyleClassSciFiTabActive)
                .PseudoPressed()
                .Box(MakeTab(active: true, pressed: true)),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiTab)
                .ParentOf(E<Label>())
                .Font(sheet.BaseFont.GetFont(13, FontKind.Bold))
                .FontColor(SciFiPalette.TextMuted),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiTab)
                .Class(SunriseStyleClass.StyleClassSciFiTabActive)
                .ParentOf(E<Label>())
                .Font(sheet.BaseFont.GetFont(13, FontKind.Bold))
                .FontColor(SciFiPalette.Accent),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiPanelButton)
                .PseudoNormal()
                .Box(MakePanelButton(active: false)),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiPanelButton)
                .PseudoHovered()
                .Box(MakePanelButton(active: false, hovered: true)),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiPanelButton)
                .PseudoPressed()
                .Box(MakePanelButton(active: false, pressed: true)),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiPanelButton)
                .PseudoDisabled()
                .Box(MakePanelButton(active: false, disabled: true)),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiPanelButton)
                .Class(SunriseStyleClass.StyleClassSciFiPanelButtonActive)
                .PseudoNormal()
                .Box(MakePanelButton(active: true)),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiPanelButton)
                .Class(SunriseStyleClass.StyleClassSciFiPanelButtonActive)
                .PseudoHovered()
                .Box(MakePanelButton(active: true, hovered: true)),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiPanelButton)
                .Class(SunriseStyleClass.StyleClassSciFiPanelButtonActive)
                .PseudoPressed()
                .Box(MakePanelButton(active: true, pressed: true)),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiPanelButton)
                .ParentOf(E<Label>())
                .Font(sheet.BaseFont.GetFont(12, FontKind.Regular))
                .FontColor(SciFiPalette.TextMuted),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiPanelButton)
                .Class(SunriseStyleClass.StyleClassSciFiPanelButtonActive)
                .ParentOf(E<Label>())
                .Font(sheet.BaseFont.GetFont(12, FontKind.Regular))
                .FontColor(SciFiPalette.Accent),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiBuyButton)
                .PseudoNormal()
                .Box(MakeBuyButton()),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiBuyButton)
                .PseudoHovered()
                .Box(MakeBuyButton(hovered: true)),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiBuyButton)
                .PseudoPressed()
                .Box(MakeBuyButton(pressed: true)),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiBuyButton)
                .PseudoDisabled()
                .Box(MakeBuyButton(disabled: true)),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiBuyButton)
                .ParentOf(E<Label>())
                .Font(sheet.BaseFont.GetFont(12, FontKind.Regular))
                .FontColor(SciFiPalette.Text),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiBuyButton)
                .PseudoHovered()
                .ParentOf(E<Label>())
                .FontColor(SciFiPalette.Accent),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiBuyButton)
                .PseudoPressed()
                .ParentOf(E<Label>())
                .FontColor(SciFiPalette.TextMuted),

            E<Button>()
                .Class(SunriseStyleClass.StyleClassSciFiBuyButton)
                .PseudoDisabled()
                .ParentOf(E<Label>())
                .FontColor(SciFiPalette.TextMuted.WithAlpha(0.65f)),

            E<RichTextLabel>()
                .Class(SunriseStyleClass.StyleClassSciFiDetailsTitle)
                .Prop(Label.StylePropertyFont, sheet.BaseFont.GetFont(16, FontKind.Bold))
                .Prop(nameof(RichTextLabel.LineHeightScale), 0.95f),

            E<PanelContainer>()
                .Class(SunriseStyleClass.SciFiDivider)
                .Panel(boxSciFiDivider)
                .MinSize(new Vector2(1, 1)),
            E<PanelContainer>().Class(StyleClass.HighDivider).Panel(boxHighDivider),
        ];
    }

    private static StyleBoxFlat MakeTabBar()
    {
        return new StyleBoxFlat
        {
            BackgroundColor = SciFiPalette.PanelBackgroundDark.WithAlpha(0.72f),
            BorderColor = SciFiPalette.BorderDim.WithAlpha(0.85f),
            BorderThickness = new Thickness(0, 0, 0, 1),
            ContentMarginLeftOverride = 0,
            ContentMarginRightOverride = 0,
            ContentMarginTopOverride = 0,
            ContentMarginBottomOverride = 0
        };
    }
    private static StyleBoxTexture MakeButton(Texture texture, bool hovered = false, bool pressed = false, bool disabled = false)
    {
        var background = SciFiPalette.AccentSoft.WithAlpha(0.93f);

        if (hovered)
        {
            background = SciFiPalette.AccentDim.WithAlpha(0.96f);
        }

        if (pressed)
        {
            background = SciFiPalette.AccentSoft.WithAlpha(0.98f);
        }

        if (disabled)
        {
            background = SciFiPalette.AccentSoft.WithAlpha(0.65f);
        }

        var styleBox = new StyleBoxTexture
        {
            Texture = texture,
            Modulate = background
        };
        styleBox.SetPatchMargin(StyleBox.Margin.All, 5);
        styleBox.SetPadding(StyleBox.Margin.All, 2);
        styleBox.SetContentMarginOverride(StyleBox.Margin.Left, 16);
        styleBox.SetContentMarginOverride(StyleBox.Margin.Right, 14);
        styleBox.SetContentMarginOverride(StyleBox.Margin.Top, 5);
        styleBox.SetContentMarginOverride(StyleBox.Margin.Bottom, 5);
        return styleBox;
    }

    private static StyleBoxTexture MakePillButton(Texture texture, bool hovered = false, bool pressed = false, bool disabled = false)
    {
        var styleBox = MakeButton(texture, hovered, pressed, disabled);
        styleBox.SetPatchMargin(StyleBox.Margin.All, 18);
        styleBox.SetContentMarginOverride(StyleBox.Margin.Left, 18);
        styleBox.SetContentMarginOverride(StyleBox.Margin.Right, 16);
        return styleBox;
    }

    private static StyleBoxTexture MakeDonateButton(Texture texture, bool hovered = false, bool pressed = false, bool disabled = false)
    {
        var background = SciFiPalette.PanelBackgroundDark.WithAlpha(0.8f);

        if (hovered)
        {
            background = SciFiPalette.AccentSoft.WithAlpha(0.38f);
        }

        if (pressed)
        {
            background = SciFiPalette.WindowBackground.WithAlpha(0.58f);
        }

        if (disabled)
        {
            background = SciFiPalette.PanelBackgroundDark.WithAlpha(0.22f);
        }

        var styleBox = new StyleBoxTexture
        {
            Texture = texture,
            Modulate = background
        };
        styleBox.SetPatchMargin(StyleBox.Margin.All, 5);
        styleBox.SetPadding(StyleBox.Margin.All, 2);
        styleBox.SetContentMarginOverride(StyleBox.Margin.Left, 12);
        styleBox.SetContentMarginOverride(StyleBox.Margin.Right, 12);
        styleBox.SetContentMarginOverride(StyleBox.Margin.Top, 5);
        styleBox.SetContentMarginOverride(StyleBox.Margin.Bottom, 5);
        return styleBox;
    }

    private static StyleBoxFlat MakeIconButton(bool hovered = false, bool pressed = false, bool disabled = false)
    {
        var background = SciFiPalette.PanelBackgroundDark.WithAlpha(0.72f);
        var border = SciFiPalette.AccentDim.WithAlpha(0.72f);

        if (hovered)
        {
            background = SciFiPalette.SlotBackground.WithAlpha(0.94f);
            border = SciFiPalette.Accent.WithAlpha(0.95f);
        }

        if (pressed)
        {
            background = SciFiPalette.WindowBackground.WithAlpha(0.98f);
            border = SciFiPalette.Border;
        }

        if (disabled)
        {
            background = SciFiPalette.PanelBackgroundDark.WithAlpha(0.58f);
            border = SciFiPalette.BorderDim.WithAlpha(0.82f);
        }

        return new StyleBoxFlat
        {
            BackgroundColor = background,
            BorderColor = border,
            BorderThickness = new Thickness(2, 1, 1, 1),
            ContentMarginLeftOverride = 6,
            ContentMarginRightOverride = 6,
            ContentMarginTopOverride = 6,
            ContentMarginBottomOverride = 6
        };
    }

    private static StyleBoxFlat MakeLineEdit()
    {
        return new StyleBoxFlat
        {
            BackgroundColor = SciFiPalette.PanelBackgroundDark,
            BorderColor = SciFiPalette.BorderDim,
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 8,
            ContentMarginRightOverride = 8,
            ContentMarginTopOverride = 5,
            ContentMarginBottomOverride = 5
        };
    }

    private static StyleBoxFlat MakeTab(bool active, bool hovered = false, bool pressed = false, bool disabled = false)
    {
        var background = active
            ? SciFiPalette.AccentSoft.WithAlpha(0.95f)
            : SciFiPalette.PanelBackgroundDark.WithAlpha(0.92f);

        var border = active
            ? SciFiPalette.Accent
            : SciFiPalette.BorderDim;

        if (hovered)
        {
            background = active
                ? SciFiPalette.AccentSoft.NudgeLightness(0.08f)
                : SciFiPalette.PanelBackground.NudgeLightness(0.04f);

            if (!active)
                border = SciFiPalette.AccentDim;
        }

        if (pressed)
        {
            background = active
                ? SciFiPalette.AccentSoft.NudgeLightness(-0.05f)
                : SciFiPalette.PanelBackgroundDark.NudgeLightness(-0.03f);

            border = active
                ? SciFiPalette.AccentDim
                : SciFiPalette.BorderDim;
        }

        if (disabled)
        {
            background = SciFiPalette.PanelBackgroundDark.WithAlpha(0.65f);
            border = SciFiPalette.BorderDim.WithAlpha(0.65f);
        }

        return new StyleBoxFlat
        {
            BackgroundColor = background,
            BorderColor = border,
            BorderThickness = active
                ? new Thickness(1, 1, 1, 0)
                : new Thickness(1),
            ContentMarginLeftOverride = 16,
            ContentMarginRightOverride = 16,
            ContentMarginTopOverride = 7,
            ContentMarginBottomOverride = active ? 8 : 7
        };
    }

    private static StyleBoxFlat MakePanelButton(bool active, bool hovered = false, bool pressed = false, bool disabled = false)
    {
        var styleBox = MakeTab(active, hovered, pressed, disabled);
        styleBox.BorderThickness = new Thickness(1);
        styleBox.ContentMarginLeftOverride = 8;
        styleBox.ContentMarginRightOverride = 8;
        styleBox.ContentMarginTopOverride = 6;
        styleBox.ContentMarginBottomOverride = 6;
        return styleBox;
    }

    private static StyleBoxFlat MakeBuyButton(bool hovered = false, bool pressed = false, bool disabled = false)
    {
        var background = SciFiPalette.PanelBackgroundDark.WithAlpha(0.95f);
        var border = SciFiPalette.AccentDim.NudgeLightness(0.12f).WithAlpha(0.95f);

        if (hovered)
        {
            background = SciFiPalette.PanelBackground.NudgeLightness(0.04f);
            border = SciFiPalette.Accent.NudgeLightness(0.04f);
        }

        if (pressed)
        {
            background = SciFiPalette.PanelBackgroundDark.NudgeLightness(-0.03f);
            border = SciFiPalette.AccentDim.WithAlpha(1f);
        }

        if (disabled)
        {
            background = SciFiPalette.PanelBackgroundDark.WithAlpha(0.65f);
            border = SciFiPalette.AccentDim.WithAlpha(0.6f);
        }

        return new StyleBoxFlat
        {
            BackgroundColor = background,
            BorderColor = border,
            BorderThickness = new Thickness(2),
            ContentMarginLeftOverride = 6,
            ContentMarginRightOverride = 6,
            ContentMarginTopOverride = 6,
            ContentMarginBottomOverride = 6
        };
    }
}
