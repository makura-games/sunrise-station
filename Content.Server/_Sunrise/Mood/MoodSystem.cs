using Content.Server.Chat.Managers;
using Content.Shared._Sunrise.Mood;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.Alert;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Mood;

public sealed partial class MoodSystem : EntitySystem
{
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeedModifier = default!;
    [Dependency] private SharedJetpackSystem _jetpack = default!;
    [Dependency] private MobThresholdSystem _mobThreshold = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MoodComponent, ComponentStartup>(OnInit);
        SubscribeLocalEvent<MoodComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<MoodComponent, MoodEffectEvent>(OnMoodEffect);
        SubscribeLocalEvent<MoodComponent, DamageChangedEvent>(OnDamageChange);
        SubscribeLocalEvent<MoodComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMoveSpeed);
        SubscribeLocalEvent<MoodComponent, MoodRemoveEffectEvent>(OnRemoveEffect);
        SubscribeLocalEvent<MoodComponent, ShowMoodEffectsAlertEvent>(OnAlertShowMoodEffectsAlert);
    }

    private void OnAlertShowMoodEffectsAlert(Entity<MoodComponent> ent, ref ShowMoodEffectsAlertEvent args)
    {
        if (args.Handled)
            return;

        AlertClicked(ent);

        args.Handled = true;
    }

    private void OnRemoveEffect(EntityUid uid, MoodComponent component, MoodRemoveEffectEvent args)
    {
        if (component.UncategorisedEffects.TryGetValue(args.EffectId, out _))
        {
            RemoveTimedOutEffect(uid, args.EffectId);
        }
        else
        {
            foreach (var (category, id) in component.CategorisedEffects)
            {
                if (id != args.EffectId)
                    continue;

                RemoveTimedOutEffect(uid, args.EffectId, category);
                return;
            }
        }
    }

    private void OnRefreshMoveSpeed(EntityUid uid, MoodComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (component.CurrentMoodThreshold is > MoodThreshold.Meh and < MoodThreshold.Good or MoodThreshold.Dead
            || _jetpack.IsUserFlying(uid))
            return;

        // This ridiculous math serves a purpose making high mood less impactful on movement speed than low mood
        var modifier =
            Math.Clamp(
                (component.CurrentMoodLevel >= component.MoodThresholds[MoodThreshold.Neutral])
                    ? _config.GetCVar(SunriseCCVars.MoodIncreasesSpeed)
                        ? MathF.Pow(1.003f, component.CurrentMoodLevel - component.MoodThresholds[MoodThreshold.Neutral])
                        : 1
                    : _config.GetCVar(SunriseCCVars.MoodDecreasesSpeed)
                        ? 2 - component.MoodThresholds[MoodThreshold.Neutral] / component.CurrentMoodLevel
                        : 1,
                component.MinimumSpeedModifier,
                component.MaximumSpeedModifier);

        args.ModifySpeed(1, modifier);
    }

    private void OnMoodEffect(EntityUid uid, MoodComponent component, MoodEffectEvent args)
    {
        if (!_config.GetCVar(SunriseCCVars.MoodEnabled)
            || !_prototypeManager.TryIndex<MoodEffectPrototype>(args.EffectId, out var prototype))
            return;

        var ev = new OnMoodEffect(uid, args.EffectId, args.EffectModifier, args.EffectOffset);
        RaiseLocalEvent(uid, ref ev);

        ApplyEffect(uid, component, prototype, ev.EffectModifier, ev.EffectOffset);
    }

    private void ApplyEffect(EntityUid uid, MoodComponent component, MoodEffectPrototype prototype, float eventModifier = 1, float eventOffset = 0)
    {
        // Apply categorised effect
        if (prototype.Category != null)
        {
            if (component.CategorisedEffects.TryGetValue(prototype.Category, out var oldPrototypeId))
            {
                if (!_prototypeManager.TryIndex<MoodEffectPrototype>(oldPrototypeId, out var oldPrototype))
                    return;

                if (prototype.ID != oldPrototype.ID)
                {
                    SendEffectText(uid, prototype);
                    component.CategorisedEffects[prototype.Category] = prototype.ID;
                }
            }
            else
            {
                component.CategorisedEffects.Add(prototype.Category, prototype.ID);
            }

            if (prototype.Timeout != 0)
                Timer.Spawn(TimeSpan.FromSeconds(prototype.Timeout), () => RemoveTimedOutEffect(uid, prototype.ID, prototype.Category));
        }
        else
        {
            if (component.UncategorisedEffects.TryGetValue(prototype.ID, out _))
                return;

            var moodChange = prototype.MoodChange * eventModifier + eventOffset;
            if (moodChange == 0)
                return;

            SendEffectText(uid, prototype);
            component.UncategorisedEffects.Add(prototype.ID, moodChange);

            if (prototype.Timeout != 0)
                Timer.Spawn(TimeSpan.FromSeconds(prototype.Timeout), () => RemoveTimedOutEffect(uid, prototype.ID));
        }

        RefreshMood(uid, component);
    }

    private void SendEffectText(EntityUid uid, MoodEffectPrototype prototype)
    {

    }

    private void RemoveTimedOutEffect(EntityUid uid, string prototypeId, string? category = null)
    {
        if (!TryComp<MoodComponent>(uid, out var comp))
            return;

        if (category == null)
        {
            if (!comp.UncategorisedEffects.ContainsKey(prototypeId))
                return;
            comp.UncategorisedEffects.Remove(prototypeId);
        }
        else
        {
            if (!comp.CategorisedEffects.TryGetValue(category, out var currentProtoId)
                || currentProtoId != prototypeId
                || !_prototypeManager.HasIndex<MoodEffectPrototype>(currentProtoId))
                return;
            comp.CategorisedEffects.Remove(category);
        }

        RefreshMood(uid, comp);
    }

    private void OnMobStateChanged(EntityUid uid, MoodComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead && args.OldMobState != MobState.Dead)
        {
            var ev = new MoodEffectEvent("Dead");
            RaiseLocalEvent(uid, ev);
        }
        else if (args.OldMobState == MobState.Dead && args.NewMobState != MobState.Dead)
        {
            var ev = new MoodRemoveEffectEvent("Dead");
            RaiseLocalEvent(uid, ev);
        }
        RefreshMood(uid, component);
    }

    private void RefreshMood(EntityUid uid, MoodComponent component)
    {
        var amount = 0f;

        foreach (var (_, protoId) in component.CategorisedEffects)
        {
            if (!_prototypeManager.TryIndex<MoodEffectPrototype>(protoId, out var prototype))
                continue;

            amount += prototype.MoodChange;
        }

        foreach (var (_, value) in component.UncategorisedEffects)
        {
            amount += value;
        }

        SetMood(uid, amount, component, refresh: true);
    }

    private void OnInit(EntityUid uid, MoodComponent component, ComponentStartup args)
    {
        if (TryComp<MobThresholdsComponent>(uid, out var mobThresholdsComponent)
            && _mobThreshold.TryGetThresholdForState(uid, MobState.Critical, out var critThreshold, mobThresholdsComponent))
            component.CritThresholdBeforeModify = critThreshold.Value;

        EnsureComp<NetMoodComponent>(uid);
        RefreshMood(uid, component);
        UpdateAppearance(uid, component);
    }

    private void SetMood(EntityUid uid, float amount, MoodComponent? component = null, bool force = false, bool refresh = false)
    {
        if (!_config.GetCVar(SunriseCCVars.MoodEnabled)
            || !Resolve(uid, ref component)
            || component.CurrentMoodThreshold == MoodThreshold.Dead && !refresh)
            return;

        var neutral = component.MoodThresholds[MoodThreshold.Neutral];
        var ev = new OnSetMoodEvent(uid, amount, false);
        RaiseLocalEvent(uid, ref ev);

        if (ev.Cancelled)
            return;
        else
        {
            uid = ev.Receiver;
            amount = ev.MoodChangedAmount;
        }

        var newMoodLevel = amount + neutral;
        if (!force)
        {
            newMoodLevel = Math.Clamp(amount + neutral,
                component.MoodThresholds[MoodThreshold.Dead],
                component.MoodThresholds[MoodThreshold.Perfect]);
        }

        component.CurrentMoodLevel = newMoodLevel;

        if (TryComp<NetMoodComponent>(uid, out var mood))
        {
            mood.CurrentMoodLevel = component.CurrentMoodLevel;
            mood.NeutralMoodThreshold = component.MoodThresholds.GetValueOrDefault(MoodThreshold.Neutral);
        }

        UpdateCurrentThreshold(uid, component);
    }

    private void UpdateCurrentThreshold(EntityUid uid, MoodComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var calculatedThreshold = GetMoodThreshold(component);
        if (calculatedThreshold == component.CurrentMoodThreshold)
            return;

        component.CurrentMoodThreshold = calculatedThreshold;

        DoMoodThresholdsEffects(uid, component);
    }

    private void DoMoodThresholdsEffects(EntityUid uid, MoodComponent? component = null, bool force = false)
    {
        if (!Resolve(uid, ref component)
            || component.CurrentMoodThreshold == component.LastThreshold && !force)
            return;

        var modifier = GetMovementThreshold(component.CurrentMoodThreshold);

        // Modify mob stats
        if (modifier != GetMovementThreshold(component.LastThreshold))
        {
            _movementSpeedModifier.RefreshMovementSpeedModifiers(uid);
            SetCritThreshold(uid, component, modifier);
            RefreshShaders(uid, modifier);
        }

        // Modify interface
        if (component.MoodThresholdsAlerts.TryGetValue(component.CurrentMoodThreshold, out var alertId))
            _alerts.ShowAlert(uid, alertId);
        else
            _alerts.ClearAlertCategory(uid, "Mood");

        // Update appearance for mood visuals
        UpdateAppearance(uid, component);

        component.LastThreshold = component.CurrentMoodThreshold;
    }

    private void RefreshShaders(EntityUid uid, int modifier)
    {
        if (modifier == -1)
            EnsureComp<SaturationScaleOverlayComponent>(uid);
        else
            RemComp<SaturationScaleOverlayComponent>(uid);
    }

    private void SetCritThreshold(EntityUid uid, MoodComponent component, int modifier)
    {
        if (!TryComp<MobThresholdsComponent>(uid, out var mobThresholds)
            || !_mobThreshold.TryGetThresholdForState(uid, MobState.Critical, out var key))
            return;

        var newKey = modifier switch
        {
            1 => FixedPoint2.New(key.Value.Float() * component.IncreaseCritThreshold),
            -1 => FixedPoint2.New(key.Value.Float() * component.DecreaseCritThreshold),
            _ => component.CritThresholdBeforeModify
        };

        component.CritThresholdBeforeModify = key.Value;
        _mobThreshold.SetMobStateThreshold(uid, newKey, MobState.Critical, mobThresholds);
    }

    private MoodThreshold GetMoodThreshold(MoodComponent component, float? moodLevel = null)
    {
        moodLevel ??= component.CurrentMoodLevel;
        var result = MoodThreshold.Dead;
        var value = component.MoodThresholds[MoodThreshold.Perfect];

        foreach (var threshold in component.MoodThresholds)
        {
            if (threshold.Value <= value && threshold.Value >= moodLevel)
            {
                result = threshold.Key;
                value = threshold.Value;
            }
        }

        return result;
    }

    private int GetMovementThreshold(MoodThreshold threshold)
    {
        return threshold switch
        {
            >= MoodThreshold.Good => 1,
            <= MoodThreshold.Meh => -1,
            _ => 0
        };
    }

    private void OnDamageChange(EntityUid uid, MoodComponent component, DamageChangedEvent args)
    {
        var totalDamage = _damageable.GetTotalDamage((uid, args.Damageable));
        if (!_mobThreshold.TryGetPercentageForState(uid, MobState.Critical, totalDamage, out var damage))
            return;

        var protoId = "HealthNoDamage";
        var value = component.HealthMoodEffectsThresholds["HealthNoDamage"];

        foreach (var threshold in component.HealthMoodEffectsThresholds)
        {
            if (!(threshold.Value <= damage) || !(threshold.Value >= value))
                continue;

            protoId = threshold.Key;
            value = threshold.Value;
        }

        var ev = new MoodEffectEvent(protoId);
        RaiseLocalEvent(uid, ev);
    }

    public void AlertClicked(EntityUid uid)
    {
        if (!TryComp<MoodComponent>(uid, out var comp)
            || comp.CurrentMoodThreshold == MoodThreshold.Dead
            || !_playerManager.TryGetSessionByEntity(uid, out var session))
            return;

        var msgStart = Loc.GetString("mood-show-effects-start");
        _chatManager.ChatMessageToOne(ChatChannel.Emotes,
            msgStart,
            msgStart,
            EntityUid.Invalid,
            false,
            session.Channel);

        foreach (var (_, protoId) in comp.CategorisedEffects)
        {
            if (!_prototypeManager.TryIndex<MoodEffectPrototype>(protoId, out var proto)
                || proto.Hidden)
                continue;

            SendDescToChat(proto, session);
        }

        foreach (var (protoId, _) in comp.UncategorisedEffects)
        {
            if (!_prototypeManager.TryIndex<MoodEffectPrototype>(protoId, out var proto)
                || proto.Hidden)
                continue;

            SendDescToChat(proto, session);
        }
    }

    private void SendDescToChat(MoodEffectPrototype proto, ICommonSession session)
    {
        var color = (proto.MoodChange > 0) ? "#008000" : "#BA0000";
        var msg = $"[font size=10][color={color}]{proto.Description}[/color][/font]";

        _chatManager.ChatMessageToOne(ChatChannel.Emotes, msg, msg, EntityUid.Invalid, false,
            session.Channel);
    }

    public void UpdateAppearance(EntityUid uid, MoodComponent? component = null, AppearanceComponent? appearance = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (!HasComp<MoodVisualsComponent>(uid))
            return;

        appearance ??= EnsureComp<AppearanceComponent>(uid);
        _appearance.SetData(uid, MoodVisuals.CurrentMoodThreshold, component.CurrentMoodThreshold, appearance);
    }
}
