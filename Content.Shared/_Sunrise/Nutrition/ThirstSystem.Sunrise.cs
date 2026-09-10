using Content.Shared.Nutrition.Components;
using Robust.Shared.GameObjects;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Shared.Nutrition.EntitySystems;

public sealed partial class ThirstSystem
{
    // Sunrise-Start
    private void OnThirstManglenessChanged(EntityUid uid, ThirstComponent component, ThirstManglenessChangedEvent args)
    {
        UpdateEffects(uid, component);
    }
    // Sunrise-End
}
