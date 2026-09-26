using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Soil;

/// <summary>
/// Компонент для мешка с землей.
/// </summary>
[RegisterComponent]
public sealed partial class SoilComponent : Component
{
    [DataField(readOnly: true)]
    public EntProtoId SpawnPrototype = "hydroponicsSoil";

    [DataField]
    public string PopupStringFailed = "soil-plant-failed";

    [DataField]
    public string PopupStringSuccess = "soil-plant-success";

    [DataField]
    public float StaminaDamage = 30f;
}
