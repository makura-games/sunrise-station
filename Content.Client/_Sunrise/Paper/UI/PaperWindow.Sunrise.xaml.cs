using System.Numerics;
using System.Text.RegularExpressions;
using Content.Client.RichText;
using Content.Client._Sunrise.UserInterface.RichText;
using Content.Client.UserInterface.Controls;
using Content.Shared.Administration;
using Content.Shared._Sunrise.Paperwork;
using Content.Shared.Paper;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.Paper.UI;

public sealed partial class PaperWindow
{
    private static readonly Type[] TemplateAllowedTags =
    [
        ..UserFormattableTags.BaseAllowedTags,
        typeof(PaperFormTagHandler),
        typeof(PaperJobTagHandler),
        typeof(PaperSignatureTagHandler),
    ];

    private static readonly Regex InteractiveInjectedAttrRegex =
        new(@"\bidx\s*=\s*(?:""[^""]*""|[^\s\]]+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex InteractiveTagEscapeRegex =
        new(@"(?<!\\)\[(?:signature|form|job)\b", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public bool TemplateFieldsEnabled { get; private set; }
    public event Action<PaperComponent.PaperTemplateRequestType, int>? OnTemplateRequested;

    private string _currentRawText = string.Empty;

    private void UpdateInteractiveTagLineHeight(float fontLineHeight)
    {
        PaperInteractiveTagHandler.FontLineHeight = fontLineHeight;
    }

    private string GetDisplayText(PaperComponent.PaperBoundUserInterfaceState state, bool isEditing)
    {
        TemplateFieldsEnabled = state.TemplateFieldsEnabled;
        _currentRawText = state.Text;

        if (TemplateFieldsEnabled && !isEditing)
            return PrepareInteractiveTagsForDisplay(state.Text);

        return EscapeInteractiveTags(state.Text);
    }

    private Type[] GetAllowedTags()
    {
        return TemplateFieldsEnabled ? TemplateAllowedTags : UserFormattableTags.BaseAllowedTags;
    }

    private void UpdateImageContent(PaperComponent.PaperBoundUserInterfaceState state)
    {
        var spriteSystem = _entitySystemManager.GetEntitySystem<SpriteSystem>();

        Robust.Client.Graphics.Texture? texture = null;
        if (state.ImageContent is { } specifier)
            texture = spriteSystem.Frame0(specifier);

        ImageContent.Visible = texture != null;
        ImageContent.Texture = texture;
        ImageContent.TextureScale = state.ImageScale ?? Vector2.One;
        ImageContent.HorizontalExpand = false;
        ImageContent.VerticalExpand = false;
    }

    private static string PrepareInteractiveTagsForDisplay(string text)
    {
        text = InjectIndexes(text, PaperInteractiveTagParsing.SignatureTagRegex, PaperInteractiveTagParsing.SignatureTagName);
        text = InjectIndexes(text, PaperInteractiveTagParsing.FormTagRegex, PaperInteractiveTagParsing.FormTagName);
        text = InjectIndexes(text, PaperInteractiveTagParsing.JobTagRegex, PaperInteractiveTagParsing.JobTagName);
        return text;
    }

    private static string InjectIndexes(string text, Regex regex, string tagName)
    {
        var idx = 0;
        return regex.Replace(text, match =>
        {
            var attrs = InteractiveInjectedAttrRegex
                .Replace(match.Groups["attrs"].Value, string.Empty)
                .Trim();

            if (attrs.EndsWith('/'))
                attrs = attrs[..^1].TrimEnd();

            if (attrs.Length > 0 && attrs[0] != '=')
                attrs = " " + attrs;

            return $"[{tagName}{attrs} idx={idx++} /]";
        });
    }

    private static string EscapeInteractiveTags(string text)
    {
        return InteractiveTagEscapeRegex.Replace(text, match => "\\" + match.Value);
    }

    internal void OnSignaturePressed(int index)
    {
        if (!TemplateFieldsEnabled)
            return;

        OnTemplateRequested?.Invoke(PaperComponent.PaperTemplateRequestType.Signature, index);
    }

    internal void OnJobPressed(int index)
    {
        if (!TemplateFieldsEnabled)
            return;

        OnTemplateRequested?.Invoke(PaperComponent.PaperTemplateRequestType.Job, index);
    }

    internal void OnFormPressed(int index)
    {
        if (!TemplateFieldsEnabled)
            return;

        var dialog = new DialogWindow(
        title: Loc.GetString("paper-form-dialog-title"),
        entries: new List<QuickDialogEntry>
        {
            new("text", QuickDialogEntryType.ShortText, Loc.GetString("paper-form-dialog-prompt")),
        });

        dialog.OnConfirmed += results =>
        {
            if (!results.TryGetValue("text", out var value) || string.IsNullOrWhiteSpace(value))
                return;

            var text = PaperInteractiveTagParsing.ReplaceNthTag(
                _currentRawText,
                PaperInteractiveTagParsing.FormTagRegex,
                index,
                FormattedMessage.EscapeText(value));

            if (text == null)
                return;

            OnSaved?.Invoke(text);
        };
    }
}
