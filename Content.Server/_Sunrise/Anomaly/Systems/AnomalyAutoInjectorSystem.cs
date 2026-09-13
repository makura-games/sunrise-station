// Система для заражения цели случайной аномалией через двухэтапный процесс:
// 1. Заражение - визуальный эффект галлюцинаций и запуск таймера
// 2. Превращение - конец таймера, применение урона и добавление компонента аномалии
// дальше блокирует повторное использование, меняет спрайт и выводит попапы (звук инъекции воспроизводится напрямую)
using Content.Shared._Sunrise.Anomaly.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.GameObjects;
using System.Collections.Generic;
using Robust.Shared.Random;
using System.Linq;
using System;
using Robust.Shared.Timing;
using Content.Shared.StatusEffectNew;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Log;
using Robust.Shared.Audio.Systems;
using Content.Shared.Humanoid;
using System.Diagnostics.CodeAnalysis;
using Content.Shared._Sunrise.Drugs;
using Content.Shared.Anomaly.Components;
using Content.Shared.Damage.Systems;

namespace Content.Server._Sunrise.Anomaly.Systems;

public sealed partial class AnomalyAutoInjectorSystem : EntitySystem
{
    private static readonly ProtoId<DamageTypePrototype> CellularDamageType = "Cellular";

    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private DamageableSystem _damageableSystem = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnomalyAutoInjectorComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private bool IsValidTargetForInjection(EntityUid target, EntityUid injector, AnomalyAutoInjectorComponent comp, [NotNullWhen(false)] out string? popup)
    {
        popup = null;

        if (!HasComp<HumanoidProfileComponent>(target))
        {
            popup = comp.PopupNotApplicable;
            return false;
        }

        if (HasComp<UsedAnomalyAutoInjectorComponent>(injector))
        {
            popup = comp.PopupNothingToInject;
            return false;
        }

        if (HasComp<PendingAnomalyInfectionComponent>(target))
        {
            popup = comp.PopupPending;
            return false;
        }

        if (HasComp<InnerBodyAnomalyComponent>(target))
        {
            popup = comp.PopupInfected;
            return false;
        }

        return true;
    }

    private void OnAfterInteract(EntityUid uid, AnomalyAutoInjectorComponent comp, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!IsValidTargetForInjection(target, uid, comp, out var popup))
        {
            if (popup != null)
                _popup.PopupEntity(Loc.GetString(popup), target, args.User);
            return;
        }

        if (comp.AnomalyTrapProtos.Count == 0)
        {
            args.Handled = true;
            return;
        }

        EnsureComp<UsedAnomalyAutoInjectorComponent>(uid);
        args.Handled = true;
        _audio.PlayPvs(comp.HypospraySound, uid);
        _statusEffects.TryAddStatusEffectDuration(target, comp.RainbowEffect, TimeSpan.FromSeconds(comp.RainbowDuration));
        if (TryComp<SeeingRainbowsWeakStatusEffectComponent>(target, out var rainbowComp))
        {
            rainbowComp.Intensity = comp.RainbowEffectIntensity;
            Dirty(target, rainbowComp);
        }

        var pending = EnsureComp<PendingAnomalyInfectionComponent>(target);
        pending.EndAt = _timing.CurTime + TimeSpan.FromSeconds(comp.AnomalyDelay);
        pending.CellularDamage = comp.CellularDamage;
        pending.SelectedAnomalyTrapProtoId = _random.Pick(comp.AnomalyTrapProtos);
    }



    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<PendingAnomalyInfectionComponent>();
        while (query.MoveNext(out var uid, out var pending))
        {
            if (pending.EndAt > now)
                continue;

            if (!Exists(uid))
                continue;

            var damage = new DamageSpecifier(_proto.Index<DamageTypePrototype>(CellularDamageType), pending.CellularDamage);
            _damageableSystem.TryChangeDamage(uid, damage);

            if (!HasComp<InnerBodyAnomalyComponent>(uid))
            {
                if (TryGetInjectionComponents(pending.SelectedAnomalyTrapProtoId!.Value, out var comps))
                {
                    EntityManager.AddComponents(uid, comps);
                }
            }

            RemCompDeferred<PendingAnomalyInfectionComponent>(uid);
        }
    }

    private bool TryGetInjectionComponents(EntProtoId protoId, [NotNullWhen(true)] out ComponentRegistry? injectionComponents)
    {
        injectionComponents = null;

        if (!_proto.TryIndex<EntityPrototype>(protoId, out var protoTrap))
            return false;

        InnerBodyAnomalyInjectorComponent? anomalyInjector = null;
        foreach (var compData in protoTrap.Components.Values)
        {
            if (compData.Component is InnerBodyAnomalyInjectorComponent injector)
            {
                anomalyInjector = injector;
                break;
            }
        }

        if (anomalyInjector == null)
            return false;

        injectionComponents = anomalyInjector.InjectionComponents;
        return true;
    }
}
