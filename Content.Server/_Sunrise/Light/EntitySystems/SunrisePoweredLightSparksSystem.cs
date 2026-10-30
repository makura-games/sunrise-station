using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Sunrise.Light.Visualizers;
using Content.Shared.Light.Components;
using Content.Shared.Power;

namespace Content.Server._Sunrise.Light.EntitySystems;

public sealed partial class SunrisePoweredLightSparksSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;

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

    private void UpdateAppearance(Entity<SunrisePoweredLightSparksComponent> ent)
    {
        if (!TryComp<AppearanceComponent>(ent, out var appearance))
            return;

        if (!TryComp<PoweredLightComponent>(ent, out var poweredLight))
            return;

        var hasPower = poweredLight.On &&
            (!TryComp<ApcPowerReceiverComponent>(ent, out var powerReceiver) || powerReceiver.Powered);

        _appearance.SetData(ent, SunrisePoweredLightVisuals.HasPower, hasPower, appearance);
    }
}
