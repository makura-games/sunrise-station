using Content.Shared.Nutrition.Components;
using Robust.Shared.GameObjects;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Shared.Nutrition.EntitySystems;

public sealed partial class HungerSystem
{
    // Sunrise-Start
    private void OnHungerManglenessChanged(EntityUid uid, HungerComponent component, HungerManglenessChangedEvent args)
    {
        DoHungerThresholdEffects(uid, component, force: true);
    }
    // Sunrise-End
}
