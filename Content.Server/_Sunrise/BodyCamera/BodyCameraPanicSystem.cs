using System.Collections.Generic;
using Content.Server.Pinpointer;
using Content.Server.Power.Components;
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
using Robust.Shared.Map;
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
    [Dependency] private IPrototypeManager _prototype = default!;
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

        // Нажавший кнопку слышит подтверждение даже при отсутствии гарнитуры
        // или невозможности доставить радиосообщение.
        if (TryComp<ActorComponent>(user, out var actor))
            recipients.Add(actor.PlayerSession);

        // Сначала отправляем сообщение штатной радиосистемой.
        _radio.SendRadioMessage(uid, message, SecurityChannel, uid);

        // Повторяем проверки радиодоставки для адресного звука.
        // Подписок на RadioReceiveEvent здесь нет, чтобы не конфликтовать
        // со штатными обработчиками радиоустройств.
        AddSecurityRadioRecipients(uid, recipients);
        if (recipients.Count > 0)
            _audio.PlayGlobal(PanicSound, Filter.Empty().AddPlayers(recipients), false);
    }

    /// Определяет получателей звука с проверками маршрутизации RadioSystem.
    /// Это отдельная проверка, а не наблюдение за фактической доставкой сообщения.
    private void AddSecurityRadioRecipients(EntityUid radioSource, HashSet<ICommonSession> recipients)
    {
        if (Deleted(radioSource))
            return;

        var channel = _prototype.Index(SecurityChannel);

        // Глобальная и направленная проверки отправки, как в RadioSystem.
        var sendAttempt = new RadioSendAttemptEvent(channel, radioSource);
        RaiseLocalEvent(ref sendAttempt);
        RaiseLocalEvent(radioSource, ref sendAttempt);
        if (sendAttempt.Cancelled)
            return;

        var sourceMapId = Transform(radioSource).MapID;
        var sourceServerExempt = HasComp<TelecomExemptComponent>(radioSource);

        // Сервер проверяется один раз и именно на карте источника.
        // GlobalReceive получателя не отменяет необходимость этого сервера.
        if (!channel.LongRange && !sourceServerExempt &&
            !HasActiveServer(sourceMapId, channel.ID))
        {
            return;
        }

        var radioQuery = EntityQueryEnumerator<ActiveRadioComponent, TransformComponent>();
        while (radioQuery.MoveNext(out var radioUid, out var radio, out var transform))
        {
            // ReceiveAllChannels обходит обе проверки каналов, как в RadioSystem.
            if (!radio.ReceiveAllChannels)
            {
                if (!radio.Channels.Contains(channel.ID) ||
                    (TryComp<IntercomComponent>(radioUid, out var intercom) &&
                     !intercom.SupportedChannels.Contains(channel.ID)))
                {
                    continue;
                }
            }

            // Для обычного канала другая карта доступна только с GlobalReceive.
            if (!channel.LongRange && transform.MapID != sourceMapId && !radio.GlobalReceive)
                continue;

            // Проверяем приём глобально и на конкретном радиоустройстве.
            var receiveAttempt = new RadioReceiveAttemptEvent(channel, radioSource, radioUid);
            RaiseLocalEvent(ref receiveAttempt);
            RaiseLocalEvent(radioUid, ref receiveAttempt);
            if (receiveAttempt.Cancelled)
                continue;

            // Штатный обработчик гарнитуры дополнительно учитывает
            // отключённые игроком каналы. Сохраняем эту проверку для звука.
            if (TryComp(radioUid, out HeadsetComponent? headset) &&
                !headset.EnabledChannels.GetValueOrDefault(channel.ID, true))
            {
                continue;
            }

            AddPanicRecipient(radioUid, recipients);
        }
    }

    /// Проверяет наличие запитанного телекоммуникационного сервера
    /// с нужным каналом на указанной карте, как в RadioSystem.HasActiveServer.
    private bool HasActiveServer(MapId mapId, string channelId)
    {
        var servers = EntityQueryEnumerator<TelecomServerComponent,
            EncryptionKeyHolderComponent, ApcPowerReceiverComponent, TransformComponent>();

        while (servers.MoveNext(out _, out _, out var keys, out var power, out var transform))
        {
            if (transform.MapID == mapId && power.Powered && keys.Channels.Contains(channelId))
                return true;
        }

        return false;
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
