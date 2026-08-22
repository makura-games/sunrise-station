using Content.Shared.Implants.Components;
using Content.Shared.Medical.CrewMonitoring;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server.Medical.CrewMonitoring;

public sealed partial class CrewMonitoringConsoleSystem
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    private void ApplyFilter(EntityUid uid, ref List<SuitSensorStatus> sensors)
    {
        if (!TryComp(uid, out CrewMonitoringFilterComponent? filter))
            return;

        var filterByDepartment = filter.AllowedDepartmentIds.Count > 0;
        var filterByHealthState = filter.AllowedHealthStates.Count > 0;
        var includeTrackers = filter.IncludeTrackers;

        if (!filterByDepartment && !filterByHealthState && includeTrackers)
            return;

        HashSet<string>? allowedDepartmentNames = null;
        if (filterByDepartment)
            allowedDepartmentNames = BuildAllowedDepartmentNameSet(filter.AllowedDepartmentIds);

        var filteredSensors = new List<SuitSensorStatus>(sensors.Count);

        foreach (var sensor in sensors)
        {
            var isTracker = IsTrackerSensor(sensor);
            if (!includeTrackers && isTracker)
                continue;

            if (filterByHealthState)
            {
                var healthState = HealthStateHelper.GetHealthState(sensor.DamagePercentage, sensor.IsAlive);
                if (!filter.AllowedHealthStates.Contains(healthState))
                    continue;
            }

            if (allowedDepartmentNames != null && !IsInAllowedDepartments(sensor, allowedDepartmentNames, includeTrackers, isTracker))
                continue;

            filteredSensors.Add(sensor);
        }

        sensors = filteredSensors;
    }

    private HashSet<string> BuildAllowedDepartmentNameSet(List<ProtoId<DepartmentPrototype>> departmentIds)
    {
        var allowedDepartments = new HashSet<string>();

        foreach (var departmentId in departmentIds)
        {
            if (_prototypeManager.TryIndex(departmentId, out var department))
                allowedDepartments.Add(Loc.GetString(department.Name));
        }

        return allowedDepartments;
    }

    private bool IsInAllowedDepartments(
        SuitSensorStatus sensor,
        HashSet<string> allowedDepartmentNames,
        bool includeTrackers,
        bool isTracker)
    {
        foreach (var department in sensor.JobDepartments)
        {
            if (allowedDepartmentNames.Contains(department))
                return true;
        }

        if (!includeTrackers)
            return false;

        return isTracker;
    }

    private bool IsTrackerSensor(SuitSensorStatus sensor)
    {
        var sensorEntity = GetEntity(sensor.SuitSensorUid);
        return HasComp<SubdermalImplantComponent>(sensorEntity);
    }
}
