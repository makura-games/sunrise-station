#pragma warning disable IDE0130 // Пространство имён vanilla-системы сохраняется для совместимости события.
namespace Content.Shared.Doors.Systems;

[ByRefEvent]
public readonly record struct DoorEmaggedEvent(EntityUid UserUid);
