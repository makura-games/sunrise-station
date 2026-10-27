using Content.Server._Sunrise.Silicons.Laws.Components;
using Content.Server.Chat.Systems;
using Content.Shared._Sunrise.Silicons.Laws.Components;
using Content.Shared.Chat;
using Content.Shared.Emag.Systems;
using Content.Shared.Popups;
using Content.Shared.Silicons.Laws;
using Content.Shared.Silicons.Laws.Components;
using Robust.Shared.Player;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Silicons.Laws;
#pragma warning restore IDE0130 // Namespace does not match folder structure

public sealed partial class SiliconLawSystem
{
    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private void InitializeSunrise()
    {
        SubscribeLocalEvent<SiliconLawProviderComponent, GotEmaggedEvent>(OnLawboardGotEmagged);
    }

    private bool TryApplyLawsetEmag(
        EntityUid uid,
        SiliconLawProviderComponent component,
        ref SiliconEmaggedEvent args)
    {
        if (args.EmagUid is not { } emagUid ||
            !TryComp<LawsetEmagComponent>(emagUid, out var lawsetEmag))
        {
            return false;
        }

        component.Laws = lawsetEmag.Lawset;
        component.Lawset = GetLawset(lawsetEmag.Lawset);
        _chatSystem.TrySendInGameICMessage(uid, Loc.GetString("borg-emagged-message"), InGameICChatType.Emote, false, isFormatted: true);
        NotifyLawsetEmagged(uid);
        EnsureComp<BlockLawChangeComponent>(uid);
        return true;
    }

    private void OnRegularEmagLawsAdded(EntityUid uid)
    {
        _chatSystem.TrySendInGameICMessage(uid, Loc.GetString("borg-emagged-message"), InGameICChatType.Emote, false, isFormatted: true);
        EnsureComp<BlockLawChangeComponent>(uid);
    }

    private void OnLawboardGotEmagged(Entity<SiliconLawProviderComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        if (HasComp<SiliconLawBoundComponent>(ent))
            return;

        if (args.EmagUid is not { } emagUid ||
            !TryComp<LawsetEmagComponent>(emagUid, out var lawsetEmag) ||
            !lawsetEmag.AffectsLawboards)
        {
            return;
        }

        ent.Comp.Laws = lawsetEmag.Lawset;
        ent.Comp.Lawset = GetLawset(lawsetEmag.Lawset);

        _popup.PopupEntity(Loc.GetString("lawboard-emag-popup"), ent.Owner, args.UserUid);

        args.Repeatable = true;
        args.Handled = true;
    }

    private void NotifyLawsetEmagged(EntityUid uid)
    {
        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        var msg = Loc.GetString("freemag-borg-freed");
        var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", msg));
        _chatManager.ChatMessageToOne(ChatChannel.Server, msg, wrappedMessage, uid, false, actor.PlayerSession.Channel,
            colorOverride: Color.LimeGreen);
    }
}
