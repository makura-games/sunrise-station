using Content.Server.Hands.Systems;
using Content.Shared.Body.Events;

namespace Content.Server._Sunrise.BloodCult.Juggernaut;

public sealed partial class JuggernautSystem : EntitySystem
{
    [Dependency] private HandsSystem _handsSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<JuggernautComponent, BodyInitEvent>(OnBodyInit);
    }

    private void OnBodyInit(EntityUid uid, JuggernautComponent component, BodyInitEvent args)
    {
        var hammer = Spawn(component.HummerSpawnId, Transform(uid).Coordinates);
        _handsSystem.TryForcePickupAnyHand(uid, hammer);
    }
}
