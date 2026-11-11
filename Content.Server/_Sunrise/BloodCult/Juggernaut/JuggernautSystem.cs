using Content.Server.Hands.Systems;

namespace Content.Server._Sunrise.BloodCult.Juggernaut;

public sealed partial class JuggernautSystem : EntitySystem
{
    [Dependency] private HandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<JuggernautComponent, MapInitEvent>(OnMapInit, after: [typeof(HandsSystem)]);
    }

    private void OnMapInit(Entity<JuggernautComponent> ent, ref MapInitEvent args)
    {
        var hammer = Spawn(ent.Comp.HummerSpawnId, Transform(ent).Coordinates);
        _hands.TryForcePickupAnyHand(ent, hammer);
    }
}
