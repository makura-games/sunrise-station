using Content.Shared._Sunrise.Movement.Carrying;
using Content.Shared.Interaction.Events;
using Content.Shared.Resist;
using Content.Shared.Throwing;
using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.Item;

/// <summary>
/// Система для обработки логики обновления таймера у <see cref="ItemRepickupCooldownComponent"/>
/// </summary>
public sealed partial class ItemRepickupCooldownSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<ItemRepickupCooldownComponent, DroppedEvent>((uid, component, args) => component.PrevDrop = _timing.CurTime);
        SubscribeLocalEvent<ItemRepickupCooldownComponent, ThrownEvent>((uid, component, args) => component.PrevDrop = _timing.CurTime);
        SubscribeLocalEvent<ItemRepickupCooldownComponent, EscapeInventoryEvent>((uid, component, args) => component.PrevDrop = _timing.CurTime);
        SubscribeLocalEvent<ItemRepickupCooldownComponent, CarryDroppedEvent>((uid, component, args) => component.PrevDrop = _timing.CurTime);
    }
}
