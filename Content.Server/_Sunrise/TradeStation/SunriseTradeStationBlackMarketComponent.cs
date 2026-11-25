using Content.Shared.Cargo.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.TradeStation;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class SunriseTradeStationBlackMarketComponent : Component
{
    [AutoPausedField]
    public TimeSpan EndTime;
    public List<ProtoId<CargoMarketPrototype>> PreviousGroups = new();
}
