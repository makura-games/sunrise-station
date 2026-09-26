using System.Numerics;
using Content.Shared._Sunrise.CopyMachine;
using Content.Shared.Labels.Components;
using Content.Shared.Paper;

namespace Content.Server._Sunrise.CopyMachine;

public sealed partial class CopyMachineSystem : EntitySystem
{
    private bool TryCopyFromSlotOrButtScan(Entity<CopyMachineComponent> ent)
    {
        var paperEntity = Spawn(ent.Comp.PaperProtoId, Transform(ent).Coordinates);
        if (!TryComp<PaperComponent>(paperEntity, out var paperComponent))
        {
            Log.Error($"{ToPrettyString(ent):entity} spawned '{ent.Comp.PaperProtoId}' without a {nameof(PaperComponent)}.");
            Del(paperEntity);
            return false;
        }

        var paper = (paperEntity, paperComponent);

        if (TryCopyButtScan(ent, paper) || TryCopyFromPaperInCopySlot(ent, paper))
            return true;

        Del(paperEntity);
        return false;
    }

    private bool TryCopyButtScan(Entity<CopyMachineComponent> ent, Entity<PaperComponent> paper)
    {
        if (!TryGetBuckledHumanoidAppearance(ent, out var humanoidAppearance))
            return false;

        if (!ProtoMan.TryIndex(humanoidAppearance.Species, out var speciesPrototype) || speciesPrototype.ButtScan == null)
            return false;

        _paper.SetImageContent(paper, speciesPrototype.ButtScan, new Vector2(15, 15));
        paper.Comp.EditingDisabled = true;
        return true;
    }

    private bool TryCopyFromPaperInCopySlot(Entity<CopyMachineComponent> ent, Entity<PaperComponent> paper)
    {
        if (!ent.Comp.CopySlot.HasItem)
            return false;

        var sourcePaperEntity = ent.Comp.CopySlot.Item;
        if (!TryComp<PaperComponent>(sourcePaperEntity, out var sourcePaperComponent))
            return false;

        _paper.SetContent(paper, sourcePaperComponent.Content);

        if (sourcePaperComponent.ImageContent is { } imageContent)
            _paper.SetImageContent(paper, imageContent, sourcePaperComponent.ImageScale);

        paper.Comp.EditingDisabled = sourcePaperComponent.EditingDisabled;

        if (sourcePaperComponent.StampState != null)
        {
            foreach (var stamp in sourcePaperComponent.StampedBy)
            {
                if (_paper.TryStamp(paper, stamp, sourcePaperComponent.StampState))
                    continue;

                Log.Warning(
                    $"Failed to copy stamp '{stamp.StampedName}' from {ToPrettyString(sourcePaperEntity):entity} to {ToPrettyString(paper.Owner):entity}.");
            }
        }

        if (TryComp<LabelComponent>(sourcePaperEntity, out var sourceLabelComponent) && !string.IsNullOrWhiteSpace(sourceLabelComponent.CurrentLabel))
            _label.Label(paper.Owner, sourceLabelComponent.CurrentLabel);

        return true;
    }
}
