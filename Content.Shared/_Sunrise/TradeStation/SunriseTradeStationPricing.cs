namespace Content.Shared._Sunrise.TradeStation;

public static class SunriseTradeStationPricing
{
    public const string SyndicateProduct = "SunriseTradeStationSyndicateSurplus";
    public static long BaseUnitPrice(int cost) => ((long) cost * 5 + 3) / 4;
}
