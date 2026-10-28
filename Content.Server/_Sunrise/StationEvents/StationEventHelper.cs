using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Station.Components;

namespace Content.Server._Sunrise.StationEvents;

/// <summary>
/// Вспомогательный класс для проверки наличия игровой станции (исключая ЦентКом).
/// </summary>
public static class StationEventHelper
{
    /// <summary>
    /// Проверяет наличие игровой станции (исключая ЦентКом).
    /// </summary>
    public static bool HasValidPlayerStation(StationSystem stationSystem, IEntityManager entityManager)
    {
        foreach (var station in stationSystem.GetStations())
        {
            if (!entityManager.TryGetComponent<StationDataComponent>(station, out var data))
                continue;

            if (data.StationConfig is null)
                continue;

            // ЦентКом не считается игровой станцией
            if (data.StationConfig.StationPrototype == "NanotrasenCentralCommand")
                continue;

            return true;
        }

        return false;
    }
}
