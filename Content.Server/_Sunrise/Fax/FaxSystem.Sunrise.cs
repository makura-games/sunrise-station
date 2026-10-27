using System.Numerics;
using Content.Server.Storage.EntitySystems;
using Content.Shared.DeviceNetwork;
using Content.Shared.Fax;
using Content.Shared.Fax.Components;
using Content.Shared.Ghost;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Labels.Components;
using Content.Shared.NameModifier.Components;
using Content.Shared.Paper;
using Content.Shared.Storage;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Fax;

public sealed partial class FaxSystem
{
    /* Передача изображений бумаги и размещение распечаток переносного факса. */
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private StorageSystem _storage = default!;
    [Dependency] private TransformSystem _transform = default!;

    private static FaxPrintout CreateSunriseNetworkPrintout(
        NetworkPayload payload,
        string content,
        string name,
        string? label,
        string? prototypeId,
        string? stampState,
        List<StampDisplayInfo>? stampedBy,
        bool locked,
        string? senderFaxName)
    {
        payload.TryGetValue(FaxConstants.FaxPaperImageData, out SpriteSpecifier? imageContent);
        Vector2? imageScale = null;
        if (payload.TryGetValue(FaxConstants.FaxPaperImageScaleData, out Vector2 transmittedScale))
            imageScale = transmittedScale;

        return new FaxPrintout(
            content,
            name,
            label,
            prototypeId,
            stampState,
            stampedBy,
            locked,
            senderFaxName,
            imageContent,
            imageScale);
    }

    private static FaxPrintout CreateSunriseFilePrintout(
        FaxFileMessage message,
        string name,
        string prototypeId)
    {
        return new FaxPrintout(
            message.Content,
            name,
            message.Label,
            prototypeId,
            imageContent: message.ImageContent,
            imageScale: message.ImageScale);
    }

    private static FaxPrintout CreateSunriseCopyPrintout(
        PaperComponent paper,
        MetaDataComponent metadata,
        NameModifierComponent? nameModifier,
        LabelComponent? label,
        FaxMachineComponent fax)
    {
        return new FaxPrintout(
            paper.Content,
            nameModifier?.BaseName ?? metadata.EntityName,
            label?.CurrentLabel,
            metadata.EntityPrototype?.ID ?? fax.PrintPaperId,
            paper.StampState,
            paper.StampedBy,
            paper.EditingDisabled,
            imageContent: paper.ImageContent,
            imageScale: paper.ImageScale);
    }

    private static void AddSunriseFaxImageData(NetworkPayload payload, PaperComponent paper)
    {
        if (paper.ImageContent == null)
            return;

        payload[FaxConstants.FaxPaperImageData] = paper.ImageContent;
        payload[FaxConstants.FaxPaperImageScaleData] = paper.ImageScale ?? Vector2.One;
    }

    private void PlaceSunrisePortableFaxPrintout(EntityUid fax, EntityUid printout)
    {
        if (!HasComp<ItemComponent>(fax))
            return;

        if (_container.TryGetContainingContainer(fax, out var parentContainer)
            && TryComp<StorageComponent>(parentContainer.Owner, out var parentStorage)
            && _storage.Insert(parentContainer.Owner, printout, out _, storageComp: parentStorage, playSound: false))
        {
            return;
        }

        if (TryComp<StorageComponent>(fax, out var faxStorage)
            && _storage.Insert(fax, printout, out _, storageComp: faxStorage, playSound: false))
        {
            return;
        }

        _transform.AttachToGridOrMap(printout);
    }

    private void ApplySunriseFaxImage(Entity<PaperComponent> paper, FaxPrintout printout)
    {
        if (printout.ImageContent != null)
            _paperSystem.SetImageContent(paper, printout.ImageContent, printout.ImageScale);
    }

    private void NotifySunriseGhostAdmins(FaxPrintout printout)
    {
        foreach (var admin in _adminManager.ActiveAdmins)
        {
            if (admin.AttachedEntity is not { } ghost || !HasComp<GhostComponent>(ghost))
                continue;

            if (!_inventory.TryGetSlotEntity(ghost, "back", out var worn)
                || !TryComp<StorageComponent>(worn.Value, out var storage))
            {
                continue;
            }

            var prototype = string.IsNullOrEmpty(printout.PrototypeId) ? "Paper" : printout.PrototypeId;
            var printed = Spawn(prototype, Transform(ghost).Coordinates);
            if (!_storage.Insert(worn.Value, printed, out _, storageComp: storage, playSound: false))
            {
                Del(printed);
                continue;
            }

            FillSunriseAdminFaxPrintout(printed, printout);
        }
    }

    private void FillSunriseAdminFaxPrintout(EntityUid printed, FaxPrintout printout)
    {
        if (TryComp<PaperComponent>(printed, out var paper))
        {
            _paperSystem.SetContent((printed, paper), printout.Content);
            ApplySunriseFaxImage((printed, paper), printout);

            if (printout.StampState != null)
            {
                foreach (var stamp in printout.StampedBy)
                {
                    _paperSystem.TryStamp((printed, paper), stamp, printout.StampState);
                }
            }

            paper.EditingDisabled = printout.Locked;
        }

        _metaData.SetEntityName(printed, printout.Name);

        if (printout.Label is { } label)
            _labelSystem.Label(printed, label);
    }
}
