using Content.Shared.FixedPoint;

// ReSharper disable once CheckNamespace
namespace Content.Server.Store.Systems;

[ByRefEvent]
public readonly record struct ItemPurchasedEvent(EntityUid Purchaser);

[ByRefEvent]
public readonly record struct SubtractCashEvent(EntityUid Purchaser, string Currency, FixedPoint2 Cost);
