using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.TradeStation;

[RegisterComponent, NetworkedComponent]
public sealed partial class SunriseTradeStationConsoleComponent : Component;

[Serializable, NetSerializable]
public enum SunriseTradeStationConsoleVisuals : byte
{
    BlackMarket,
}
