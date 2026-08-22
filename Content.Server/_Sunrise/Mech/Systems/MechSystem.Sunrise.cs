using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.Mech.Systems;

public sealed partial class MechSystem
{
    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private MobThresholdSystem _mobThresholdSystem = default!;

    private static readonly ProtoId<DamageTypePrototype> ManglenessDamageType = "Mangleness";

    private void InitializeSunrise()
    {
        SubscribeLocalEvent<MechComponent, MechSayEvent>(OnMechSay);
    }

    private void OnMechSay(EntityUid uid, MechComponent component, MechSayEvent args)
    {
        _chatSystem.TrySendInGameICMessage(uid,
            Loc.GetString(args.Message),
            InGameICChatType.Whisper,
            ChatTransmitRange.Normal);
    }

    /// <summary>
    /// Переносит существующий порог критического состояния в новое поле прочности меха.
    /// </summary>
    private void SetSunriseMaxIntegrity(EntityUid uid, MechComponent component)
    {
        if (TryComp<MobThresholdsComponent>(uid, out var thresholds)
            && _mobThresholdSystem.TryGetThresholdForState(uid, MobState.Critical, out var threshold, thresholds)
            && threshold is { } maxIntegrity)
        {
            component.MaxIntegrity = maxIntegrity;
        }
    }

    private void SaySunriseCritMessage(EntityUid uid,
        MechComponent component,
        FixedPoint2 totalDamage,
        bool damageIncreased)
    {
        if (!damageIncreased || component.MaxIntegrity <= 0)
            return;

        var damagePercentage = totalDamage / component.MaxIntegrity * 100;
        MechHealthState newState;

        if (damagePercentage >= 95)
            newState = MechHealthState.Critical;
        else if (damagePercentage >= 50)
            newState = MechHealthState.Damaged;
        else
            newState = MechHealthState.Healthy;

        if (newState == component.HealthState)
            return;

        component.HealthState = newState;
        Dirty(uid, component);

        var message = newState switch
        {
            MechHealthState.Critical => component.MessageAlert5,
            MechHealthState.Damaged => component.MessageAlert50,
            _ => string.Empty,
        };

        if (string.IsNullOrEmpty(message))
            return;

        var chatType = newState == MechHealthState.Critical
            ? InGameICChatType.Speak
            : InGameICChatType.Whisper;

        _chatSystem.TrySendInGameICMessage(uid,
            Loc.GetString(message),
            chatType,
            ChatTransmitRange.Normal);
    }

    private static void RemoveSunrisePilotDamage(DamageSpecifier damage)
    {
        damage.DamageDict.Remove(ManglenessDamageType);
    }
}
