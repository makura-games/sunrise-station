using System.Globalization;
using Content.Server._Sunrise.Chat.Sanitization;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Chat;
using Content.Shared.PDA;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Utility;

#pragma warning disable IDE0130 // Namespace не соответствует расположению файла
namespace Content.Server.Radio.EntitySystems;

public sealed partial class RadioSystem
{
    [Dependency] private AccessReaderSystem _accessReader = default!;

    private const string NoIdIconPath = "/Textures/Interface/Misc/job_icons.rsi/NoId.png";
    private const string StationAiIconPath = "/Textures/Interface/Misc/job_icons.rsi/StationAi.png";
    private const string BorgIconPath = "/Textures/_Sunrise/Interface/Misc/job_icons.rsi/Borg.png";

    private bool TrySanitizeRadioMessage(EntityUid source, ref string message)
    {
        var ev = new TrySendChatMessageEvent(message, InGameICChatType.Speak, ProcessUserInput: false);
        RaiseLocalEvent(source, ref ev);

        if (ev.Cancelled)
            return false;

        message = ev.Message;
        return true;
    }

    private string GetRadioDisplayName(EntityUid sender, string name)
    {
        var tag = Loc.GetString("radio-icon-tag",
            ("path", GetIdSprite(sender)),
            ("scale", "3"),
            ("text", GetIdCardName(sender)),
            ("color", GetIdCardColor(sender)));

        return $"{tag} {name}";
    }

    private string FormatRadioContent(EntityUid sender, string content)
    {
        return GetIdCard(sender)?.RadioBold == true
            ? $"[bold]{content}[/bold]"
            : content;
    }

    private IdCardComponent? GetIdCard(EntityUid sender)
    {
        if (!_accessReader.FindAccessItemsInventory(sender, out var accessItems))
            return null;

        foreach (var item in accessItems)
        {
            if (TryComp<PdaComponent>(item, out var pda) &&
                pda.ContainedId is { } containedId &&
                TryComp<IdCardComponent>(containedId, out var containedCard))
            {
                return containedCard;
            }

            if (TryComp<IdCardComponent>(item, out var card))
                return card;
        }

        return null;
    }

    private string GetIdCardName(EntityUid sender)
    {
        var title = GetIdCard(sender)?.LocalizedJobTitle ?? Loc.GetString("chat-radio-no-id");
        return $"[{CultureInfo.CurrentCulture.TextInfo.ToTitleCase(title)}] ";
    }

    private string GetIdCardColor(EntityUid sender)
    {
        var color = GetIdCard(sender)?.JobColor;
        return string.IsNullOrEmpty(color) ? "#9FED58" : color;
    }

    private string GetIdSprite(EntityUid sender)
    {
        if (HasComp<BorgChassisComponent>(sender))
            return BorgIconPath;

        if (HasComp<StationAiHeldComponent>(sender))
            return StationAiIconPath;

        var prototypeId = GetIdCard(sender)?.JobIcon;
        if (!ProtoMan.TryIndex(prototypeId, out var prototype))
            return NoIdIconPath;

        return prototype.Icon switch
        {
            SpriteSpecifier.Texture texture => texture.TexturePath.CanonPath,
            SpriteSpecifier.Rsi rsi => $"{rsi.RsiPath.CanonPath}/{rsi.RsiState}.png",
            _ => NoIdIconPath,
        };
    }
}
