using Content.Shared.FixedPoint;

namespace Content.Server._Sunrise.TradeStation;

[RegisterComponent]
public sealed partial class SunriseTradeStationComputerComponent : Component
{
    [DataField]
    public FixedPoint2 BreakDamage = 100;

    [DataField]
    public FixedPoint2 DestructionDamage = 1000;
}
