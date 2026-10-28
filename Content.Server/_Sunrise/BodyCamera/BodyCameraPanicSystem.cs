using System.Collections.Generic;
using Content.Server.Pinpointer;
using Content.Server.Radio;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Access.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.Interaction;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Sunrise.BodyCamera;

/// Отправляет ручной экстренный вызов в Security через стандартное действие камеры.
public sealed partial class BodyCameraPanicSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private NavMapSystem _navMap = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly ProtoId<RadioChannelPrototype> SecurityChannel = "Security";
    private static readonly SoundSpecifier PanicSound =
        new SoundPathSpecifier("/Audio/_Sunrise/Items/Equip/BodyCamera/panic_button.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyCameraPanicComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnGetVerbs(EntityUid uid, BodyCameraPanicComponent component, GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !HasComp<ActorComponent>(args.User))
            return;

        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("body-camera-panic-verb"),
            Disabled = _timing.CurTime < component.NextActivation,
            Act = () => TrySendPanic(uid, user),
        });
    }

    /// Повторно проверяет доступность камеры и отправляет вызов с актуальными данными игрока.
    private void TrySendPanic(EntityUid uid, EntityUid user)
    {
        if (!CanSendPanic(uid, user, out var component))
            return;

        var jobTitle = Loc.GetString("body-camera-panic-unknown-job");
        if (_idCard.TryFindIdCard(user, out var idCard) &&
            !string.IsNullOrWhiteSpace(idCard.Comp.LocalizedJobTitle))
        {
            jobTitle = idCard.Comp.LocalizedJobTitle;
        }

        // Убираем пробелы по краям и начинаем должность с заглавной буквы.
        jobTitle = jobTitle.Trim();
        if (!string.IsNullOrEmpty(jobTitle))
            jobTitle = char.ToUpperInvariant(jobTitle[0]) + jobTitle[1..];

        // Имя берётся у персонажа, а не у предъявленной ID-карты.
        var characterName = MetaData(user).EntityName;
        var position = FormattedMessage.RemoveMarkupOrThrow(_navMap.GetNearestBeaconString(user));
        var message = Loc.GetString("body-camera-panic-message",
            ("job", jobTitle),
            ("name", characterName),
            ("position", position));

        component.NextActivation = _timing.CurTime + component.Cooldown;

        // Как у RattleOnTriggerSystem, источник радиопередачи — устройство.
        // Messenger намеренно не используется: вызов направляется только в Security.
        var recipients = new HashSet<ICommonSession>();

        // Нажавший кнопку слышит подтерждение даже при отсутствии гарнитуры.
        if (TryComp<ActorComponent>(user, out var actor))
            recipients.Add(actor.PlayerSession);

        // Sunrise edit start: получатели выбираются только штатной радиосистемой.
        // Колбэк относится к этому вызову и не повторяет события попытки доставки.
        _radio.SendRadioMessage(uid, message, SecurityChannel, uid,
            onReceiver: receiver => AddSecurityRadioRecipients(receiver, recipients));
        // Sunrise edit end
        if (recipients.Count > 0)
            _audio.PlayGlobal(PanicSound, Filter.Empty().AddPlayers(recipients), false);
    }

    /// Добавляет владельца радиоустройства, уже выбранного штатной радиопередачей.
    private void AddSecurityRadioRecipients(EntityUid receiver, HashSet<ICommonSession> recipients)
    {
        // HeadsetSystem может отклонить выбранное устройство при выключенном канале.
        // Это проверка воспроизведения гарнитуры, а не повтор маршрутизации RadioSystem.
        if (TryComp(receiver, out HeadsetComponent? headset) &&
            !headset.EnabledChannels.GetValueOrDefault(SecurityChannel.Id, true))
        {
            return;
        }

        AddPanicRecipient(receiver, recipients);
    }

    private void AddPanicRecipient(EntityUid uid, HashSet<ICommonSession> recipients)
    {
        while (uid.IsValid() && !Deleted(uid))
        {
            if (TryComp<ActorComponent>(uid, out var actor))
            {
                recipients.Add(actor.PlayerSession);
                return;
            }

            if (!TryComp(uid, out TransformComponent? transform) || transform.ParentUid == uid)
                return;

            uid = transform.ParentUid;
        }
    }

    private bool CanSendPanic(EntityUid uid, EntityUid user, out BodyCameraPanicComponent component)
    {
        component = default!;

        if (Deleted(uid) || Deleted(user) ||
            !TryComp(uid, out BodyCameraPanicComponent? panic) ||
            !HasComp<ActorComponent>(user) ||
            _timing.CurTime < panic.NextActivation ||
            !_actionBlocker.CanInteract(user, uid) ||
            !_interaction.InRangeAndAccessible(user, uid))
        {
            return false;
        }

        component = panic;
        return true;
    }
}
