using System.Numerics;
using Content.Shared.Access.Systems;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Hands.EntitySystems;
using Content.Shared._Sunrise.Paperwork;
using Robust.Shared.Utility;

namespace Content.Shared.Paper;

public sealed partial class PaperSystem
{
    [Dependency] private SharedIdCardSystem _idCard = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    private static readonly Vector2 DefaultImageScale = new(1f, 1f);

    partial void InitializeTemplateFieldSupport()
    {
        SubscribeLocalEvent<PaperComponent, PaperComponent.PaperTemplateRequestMessage>(OnTemplateRequest);
    }

    private bool TryValidateWriteTool(Entity<PaperComponent> paper, EntityUid user)
    {
        if (_hands.GetActiveItem(user) is not { } used)
        {
            _popupSystem.PopupEntity(Loc.GetString("paper-no-write-tool"), paper, user);
            return false;
        }

        if (!_tagSystem.HasTag(used, WriteTag))
        {
            _popupSystem.PopupEntity(Loc.GetString("paper-no-write-tool"), paper, user);
            return false;
        }

        if (paper.Comp.StampedBy.Count > 0 && !_tagSystem.HasTag(used, WriteIgnoreStampsTag))
        {
            _popupSystem.PopupEntity(Loc.GetString("paper-stamped-write-blocked"), paper, user);
            return false;
        }

        return true;
    }

    private void OnTemplateRequest(Entity<PaperComponent> entity, ref PaperComponent.PaperTemplateRequestMessage args)
    {
        switch (args.Type)
        {
            case PaperComponent.PaperTemplateRequestType.Signature:
                HandleSignatureRequest(entity, args.Actor, args.Index);
                break;
            case PaperComponent.PaperTemplateRequestType.Job:
                HandleJobRequest(entity, args.Actor, args.Index);
                break;
        }
    }

    private bool TryValidateTemplateWriteRequest(Entity<PaperComponent> entity, EntityUid actor, int index)
    {
        if (!PaperInteractiveTagParsing.ContainsInteractiveTags(entity.Comp.Content))
            return false;

        if (!TryValidateWriteTool(entity, actor))
            return false;

        var ev = new PaperWriteAttemptEvent(entity.Owner);
        RaiseLocalEvent(actor, ref ev);

        return !ev.Cancelled && index >= 0;
    }

    private void HandleSignatureRequest(Entity<PaperComponent> entity, EntityUid actor, int index)
    {
        if (!TryValidateTemplateWriteRequest(entity, actor, index))
            return;

        var signature = FormattedMessage.EscapeText(Name(actor));
        var newContent = PaperInteractiveTagParsing.ReplaceNthTag(
            entity.Comp.Content,
            PaperInteractiveTagParsing.SignatureTagRegex,
            index,
            signature);

        if (newContent == null || newContent.Length > entity.Comp.ContentSize)
            return;

        SetContent(entity, newContent);
        _audio.PlayPvs(entity.Comp.Sound, entity);
        _adminLogger.Add(
            LogType.Chat,
            LogImpact.Low,
            $"{ToPrettyString(actor):player} has signed {ToPrettyString(entity):entity} with: {signature}");
    }

    private void HandleJobRequest(Entity<PaperComponent> entity, EntityUid actor, int index)
    {
        if (!TryValidateTemplateWriteRequest(entity, actor, index))
            return;

        var jobTitle = Loc.GetString("paper-job-unknown");
        if (_idCard.TryFindIdCard(actor, out var idCard))
        {
            var title = idCard.Comp.LocalizedJobTitle;
            if (!string.IsNullOrWhiteSpace(title))
                jobTitle = title.Trim();
        }

        var jobEscaped = FormattedMessage.EscapeText(jobTitle);
        var newContent = PaperInteractiveTagParsing.ReplaceNthTag(
            entity.Comp.Content,
            PaperInteractiveTagParsing.JobTagRegex,
            index,
            jobEscaped);

        if (newContent == null || newContent.Length > entity.Comp.ContentSize)
            return;

        SetContent(entity, newContent);
        _audio.PlayPvs(entity.Comp.Sound, entity);
        _adminLogger.Add(
            LogType.Chat,
            LogImpact.Low,
            $"{ToPrettyString(actor):player} has filled job on {ToPrettyString(entity):entity} with: {jobEscaped}");
    }

    public void SetImageContent(Entity<PaperComponent> entity, SpriteSpecifier content, Vector2? scale = null)
    {
        entity.Comp.ImageContent = content;
        entity.Comp.ImageScale = scale ?? DefaultImageScale;
        Dirty(entity);
        UpdateUserInterface(entity);
    }

    private PaperComponent.PaperBoundUserInterfaceState GetPaperUiState(Entity<PaperComponent> entity)
    {
        var templateFieldsEnabled = PaperInteractiveTagParsing.ContainsInteractiveTags(entity.Comp.Content);

        return new PaperComponent.PaperBoundUserInterfaceState(
            entity.Comp.Content,
            entity.Comp.DefaultColor,
            entity.Comp.StampedBy,
            entity.Comp.Mode,
            entity.Comp.ImageContent,
            entity.Comp.ImageScale,
            templateFieldsEnabled);
    }
}
