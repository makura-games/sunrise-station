using Content.Server.Atmos.Components;
using Content.Server.Electrocution;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Mobs.Components;
using Content.Shared.Power.Components;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    [Dependency] private ElectrocutionSystem _electrocution = default!;
    [Dependency] private BatterySystem _battery = default!;
    [Dependency] private PowerReceiverSystem _powerReceiver = default!;

    [Dependency] private EntityQuery<ApcPowerReceiverComponent> _powerReceiverQuery = default!;
    [Dependency] private EntityQuery<MobStateComponent> _mobQuery = default!;
    [Dependency] private EntityQuery<BatteryComponent> _batteryQuery = default!;
    [Dependency] private EntityQuery<ChargedElectrovaeAffectedComponent> _chargedElectrovaeQuery = default!;

    private void InitializeChargedElectrovaeSunrise()
    {
        InitializeChargedElectrovae();

    }
}
