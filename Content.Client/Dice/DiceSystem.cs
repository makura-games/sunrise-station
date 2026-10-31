using Content.Shared.Dice;
using Robust.Client.GameObjects;

namespace Content.Client.Dice;

public sealed partial class DiceSystem : SharedDiceSystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    [SubscribeLocalEvent]
    private void OnDiceAfterHandleState(Entity<DiceComponent> entity, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<SpriteComponent>(entity, out var sprite))
            return;

        UpdateSunriseDiceSprite(entity, sprite); // Sunrise-Edit - настраиваемый кубик не имеет состояний для каждой грани.
    }
}
