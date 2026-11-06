using Content.Server.Power.Components;
using Content.Shared._Sunrise.Light.Visualizers;
using Content.Shared.Light;
using Content.Shared.Light.Components;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.Light.EntitySystems;

public sealed partial class SunrisePoweredLightSparksSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SunrisePoweredLightSparksComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SunrisePoweredLightSparksComponent, SunrisePoweredLightSparksUpdatedEvent>(OnPoweredLightUpdated);
    }

    private void OnStartup(Entity<SunrisePoweredLightSparksComponent> ent, ref ComponentStartup args)
    {
        UpdateAppearance(ent);
    }

    private void OnPoweredLightUpdated(Entity<SunrisePoweredLightSparksComponent> ent, ref SunrisePoweredLightSparksUpdatedEvent args)
    {
        UpdateAppearance(ent);
    }

    private void UpdateAppearance(Entity<SunrisePoweredLightSparksComponent> ent)
    {
        if (!TryComp<AppearanceComponent>(ent, out var appearance))
            return;

        if (!TryComp<PoweredLightComponent>(ent, out var poweredLight))
            return;

        var hasPower = poweredLight.On &&
            (!TryComp<ApcPowerReceiverComponent>(ent, out var powerReceiver) || powerReceiver.Powered);

        var isBroken = _appearance.TryGetData<PoweredLightState>(ent, PoweredLightVisuals.BulbState, out var state, appearance) &&
            state == PoweredLightState.Broken;

        if (isBroken)
            ent.Comp.ShouldFlicker ??= _random.Prob(ent.Comp.FlickerChance);
        else
            ent.Comp.ShouldFlicker = null;

        _appearance.SetData(ent, SunrisePoweredLightVisuals.HasPower, hasPower, appearance);
        _appearance.SetData(ent, SunrisePoweredLightVisuals.ShouldFlicker, ent.Comp.ShouldFlicker == true, appearance);
    }
}
