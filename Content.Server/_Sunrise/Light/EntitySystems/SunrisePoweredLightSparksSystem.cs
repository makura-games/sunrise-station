using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Sunrise.Light.Visualizers;
using Content.Shared.Light;
using Content.Shared.Light.Components;
using Content.Shared.Power;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Light.EntitySystems;

public sealed class SunrisePoweredLightSparksSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SunrisePoweredLightSparksComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SunrisePoweredLightSparksComponent, ExtensionCableSystem.ProviderConnectedEvent>(OnProviderConnected);
        SubscribeLocalEvent<SunrisePoweredLightSparksComponent, ExtensionCableSystem.ProviderDisconnectedEvent>(OnProviderDisconnected);
        SubscribeLocalEvent<SunrisePoweredLightSparksComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<SunrisePoweredLightSparksComponent, SunrisePoweredLightSparksUpdatedEvent>(OnPoweredLightUpdated);
    }

    private void OnStartup(Entity<SunrisePoweredLightSparksComponent> ent, ref ComponentStartup args)
    {
        UpdateAppearance(ent);
    }

    private void OnProviderConnected(Entity<SunrisePoweredLightSparksComponent> ent, ref ExtensionCableSystem.ProviderConnectedEvent args)
    {
        UpdateAppearance(ent);
    }

    private void OnProviderDisconnected(Entity<SunrisePoweredLightSparksComponent> ent, ref ExtensionCableSystem.ProviderDisconnectedEvent args)
    {
        UpdateAppearance(ent);
    }

    private void OnPowerChanged(Entity<SunrisePoweredLightSparksComponent> ent, ref PowerChangedEvent args)
    {
        UpdateAppearance(ent);
    }

    private void OnPoweredLightUpdated(Entity<SunrisePoweredLightSparksComponent> ent, ref SunrisePoweredLightSparksUpdatedEvent args)
    {
        UpdateAppearance(ent);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<ActiveSunrisePoweredLightSparksComponent, SunrisePoweredLightSparksComponent>();
        while (query.MoveNext(out var uid, out var active, out var sparks))
        {
            if (_timing.CurTime < active.NextFlickerTime)
                continue;

            StartFlicker((uid, sparks));
            active.NextFlickerTime = GetNextFlickerTime(sparks);
        }
    }

    private void UpdateAppearance(Entity<SunrisePoweredLightSparksComponent> ent)
    {
        if (!TryComp<AppearanceComponent>(ent, out var appearance))
            return;

        if (!TryComp<PoweredLightComponent>(ent, out var poweredLight))
            return;

        var hasPower = poweredLight.On &&
            (!TryComp<ApcPowerReceiverComponent>(ent, out var powerReceiver) || powerReceiver.Powered);

        _appearance.SetData(ent, SunrisePoweredLightVisuals.HasPower, hasPower, appearance);

        if (!_appearance.TryGetData<PoweredLightState>(ent, PoweredLightVisuals.BulbState, out var bulbState, appearance) ||
            bulbState != PoweredLightState.Broken)
        {
            ent.Comp.ShouldFlicker = null;
            RemComp<ActiveSunrisePoweredLightSparksComponent>(ent);
            return;
        }

        if (!hasPower)
        {
            RemComp<ActiveSunrisePoweredLightSparksComponent>(ent);
            return;
        }

        ent.Comp.ShouldFlicker ??= _random.Prob(ent.Comp.FlickerChance);
        if (ent.Comp.ShouldFlicker != true)
        {
            RemComp<ActiveSunrisePoweredLightSparksComponent>(ent);
            return;
        }

        var active = EnsureComp<ActiveSunrisePoweredLightSparksComponent>(ent);
        if (active.NextFlickerTime == TimeSpan.Zero)
            active.NextFlickerTime = _timing.CurTime;
    }

    private void StartFlicker(Entity<SunrisePoweredLightSparksComponent> ent)
    {
        if (!TryComp<AppearanceComponent>(ent, out var appearance))
            return;

        if (ent.Comp.FlickerSound != null)
            _audio.PlayPvs(ent.Comp.FlickerSound, ent);

        var showSparks = ent.Comp.SparkStates.Count > 0 && _random.Prob(ent.Comp.SparksChance);
        _appearance.SetData(ent, SunrisePoweredLightVisuals.FlickerState, _random.Pick(ent.Comp.States), appearance);
        _appearance.SetData(ent, SunrisePoweredLightVisuals.ShowSparks, showSparks, appearance);
        if (showSparks)
            _appearance.SetData(ent, SunrisePoweredLightVisuals.SparkState, _random.Pick(ent.Comp.SparkStates), appearance);
        _appearance.SetData(ent, SunrisePoweredLightVisuals.FlickerSequence, ++ent.Comp.FlickerSequence, appearance);
    }

    private TimeSpan GetNextFlickerTime(SunrisePoweredLightSparksComponent component)
    {
        var min = component.MinFlickerDelay.TotalSeconds;
        var max = Math.Max(min, component.MaxFlickerDelay.TotalSeconds);
        var delay = min + (max - min) * _random.NextDouble();
        return _timing.CurTime + TimeSpan.FromSeconds(delay);
    }
}

[RegisterComponent]
public sealed partial class ActiveSunrisePoweredLightSparksComponent : Component
{
    /// <summary>
    /// Время следующей вспышки поврежденного светильника.
    /// </summary>
    public TimeSpan NextFlickerTime;
}
