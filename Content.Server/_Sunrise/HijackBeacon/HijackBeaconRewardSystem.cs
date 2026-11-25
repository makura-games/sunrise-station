using Content.Shared.HijackBeacon;

namespace Content.Server._Sunrise.HijackBeacon;

public sealed class HijackBeaconRewardSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HijackBeaconComponent, HijackBeaconRewardEvent>(OnReward);
    }

    private void OnReward(Entity<HijackBeaconComponent> ent, ref HijackBeaconRewardEvent args)
    {
        Spawn("SunriseBriefcaseHijackReward", Transform(ent).Coordinates);
    }
}
