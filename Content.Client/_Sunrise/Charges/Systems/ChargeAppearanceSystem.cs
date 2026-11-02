using Content.Client.Charges;
using Content.Shared.Charges;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Robust.Client.GameObjects;

namespace Content.Client._Sunrise.Charges.Systems;

/// <summary>
/// Динамическое обновление цвета иконок в зависимости от количества зарядов.
/// </summary>
/// Стереть в пыль если добавят оффы
public sealed partial class ChargeAppearanceSystem : EntitySystem
{
    [Dependency] private SharedChargesSystem _charges = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(ChargesSystem));
        UpdatesBefore.Add(typeof(AppearanceSystem));
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = AllEntityQuery<AutoRechargeComponent, LimitedChargesComponent, AppearanceComponent>();
        while (query.MoveNext(out var uid, out var recharge, out var charges, out var appearance))
        {
            var current = _charges.GetCurrentCharges((uid, charges, recharge));
            _appearance.SetData(uid, LimitedChargesState.HasCharges, current != 0, appearance);
        }
    }
}
