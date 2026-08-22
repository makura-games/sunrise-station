using Content.Shared.FixedPoint;

#pragma warning disable IDE0130 // Пространство имён соответствует расширяемой системе.
namespace Content.Server.Store.Systems;

[ByRefEvent]
public readonly record struct ItemPurchasedEvent(EntityUid Purchaser);

[ByRefEvent]
public readonly record struct SubtractCashEvent(EntityUid Purchaser, string Currency, FixedPoint2 Cost);
