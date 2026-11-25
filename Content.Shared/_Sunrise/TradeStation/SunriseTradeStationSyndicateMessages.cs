using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.TradeStation;

[Serializable, NetSerializable]
public sealed class SunriseTradeStationSyndicateRequest : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class SunriseTradeStationSyndicateResponse(bool allowed) : BoundUserInterfaceMessage
{
    public bool Allowed = allowed;
}
