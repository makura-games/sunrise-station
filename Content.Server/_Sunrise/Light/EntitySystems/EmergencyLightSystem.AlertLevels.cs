using Content.Server.AlertLevel;
using Content.Server.Light.Components;
using Content.Shared.Light;
using Content.Shared.Light.Components;
using Content.Shared.Station.Components;
using Robust.Server.GameObjects;

#pragma warning disable IDE0130 // Пространство имён соответствует расширяемой upstream-системе.
namespace Content.Server.Light.EntitySystems;

public sealed partial class EmergencyLightSystem
{
    /*
     * Emergency-light updates for the highest-priority active alert level.
     */

    [Dependency] private readonly AlertLevelSystem _alertLevel = default!;

    private void OnAdditionalAlertLevelChanged(AdditionalAlertLevelChangedEvent ev)
    {
        UpdateStationLights(ev.Station);
    }

    private void UpdateStationLights(EntityUid station)
    {
        if (!_alertLevel.TryGetVisualAlertLevel((station, null), out _, out var details))
            return;

        var query = EntityQueryEnumerator<EmergencyLightComponent, PointLightComponent, AppearanceComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var light, out var pointLight, out var appearance, out var transform))
        {
            if (CompOrNull<StationMemberComponent>(transform.GridUid)?.Station != station)
                continue;

            _pointLight.SetColor(uid, details.EmergencyLightColor, pointLight);
            _appearance.SetData(uid, EmergencyLightVisuals.Color, details.EmergencyLightColor, appearance);

            if (details.ForceEnableEmergencyLights && !light.ForciblyEnabled)
            {
                light.ForciblyEnabled = true;
                TurnOn((uid, light));
            }
            else if (!details.ForceEnableEmergencyLights && light.ForciblyEnabled)
            {
                light.ForciblyEnabled = false;
                UpdateState((uid, light));
            }
        }
    }
}
