using Content.Shared._Sunrise.Antags.Vampires.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Ensnaring;
using Content.Shared.Ensnaring.Components;
using Content.Shared.Flash;
using Content.Shared.Humanoid;
using Content.Shared.Light.Components;
using Content.Shared.StepTrigger.Systems;
using Content.Server.Light.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Sunrise.Antags.Vampires.Systems;

public sealed partial class ShadowSnareSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedEnsnareableSystem _ensnare = default!;
    [Dependency] private SharedFlashSystem _flash = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private PoweredLightSystem _poweredLightSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShadowSnareComponent, StepTriggerAttemptEvent>(OnShadowSnareStepAttempt);
        SubscribeLocalEvent<ShadowSnareComponent, StepTriggeredOffEvent>(OnShadowSnareTriggered);
        SubscribeLocalEvent<ShadowSnareComponent, AfterFlashedEvent>(OnShadowSnareFlashed);
    }

    private void OnShadowSnareStepAttempt(EntityUid uid, ShadowSnareComponent component, ref StepTriggerAttemptEvent args)
        => args.Continue = true;

    private void OnShadowSnareTriggered(EntityUid uid, ShadowSnareComponent component, ref StepTriggeredOffEvent args)
    {
        var target = args.Tripper;

        // Срабатываем только на гуманоидов
        if (!HasComp<HumanoidProfileComponent>(target))
            return;

        // Не срабатываем на вампиров и тхраллов
        if (HasComp<VampireComponent>(target) || HasComp<VampireThrallComponent>(target))
            return;

        // Наносим физический урон
        _damageable.TryChangeDamage(target, component.Damage, true, origin: uid);

        // Налагаем временную слепоту через систему вспышек
        var blindDuration = TimeSpan.FromSeconds(component.BlindDuration);
        _flash.Flash(target, null, null, blindDuration, slowTo: 1f, displayPopup: false);

        // Гасим ближайшие источники света
        ExtinguishNearbyLights(uid, component.LightExtinguishRadius);

        // Спавним сущность-ловушку и применяем к цели
        var ensnareEnt = Spawn(component.EnsnarePrototype, Transform(target).Coordinates);
        if (TryComp<EnsnaringComponent>(ensnareEnt, out var ensnaring))
        {
            ensnaring.WalkSpeed = component.WalkSpeed;
            ensnaring.SprintSpeed = component.SprintSpeed;
            ensnaring.FreeTime = component.FreeTime;
            ensnaring.BreakoutTime = component.BreakoutTime;
            _ensnare.TryEnsnare(target, ensnareEnt, ensnaring);
        }

        // Проигрываем звук срабатывания
        _audio.PlayPvs(component.TriggerSound, uid, AudioParams.Default.WithVolume(1f));

        QueueDel(uid);
    }

    private void OnShadowSnareFlashed(EntityUid uid, ShadowSnareComponent component, ref AfterFlashedEvent args)
        => QueueDel(uid);

    private void ExtinguishNearbyLights(EntityUid uid, float radius)
    {
        var center = Transform(uid).Coordinates;

        foreach (var ent in _lookup.GetEntitiesInRange(center, radius))
        {
            if (TryComp<PoweredLightComponent>(ent, out var light))
                _poweredLightSystem.SetState(ent, false, light);
        }
    }
}
