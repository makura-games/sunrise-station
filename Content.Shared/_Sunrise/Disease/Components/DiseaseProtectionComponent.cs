using Content.Shared._Sunrise.Disease.Prototypes;

namespace Content.Shared._Sunrise.Disease.Components;

/// <summary>
/// Снижает шанс заражения по указанным путям. Работает и на надетом снаряжении, и на самом носителе.
/// </summary>
[RegisterComponent]
public sealed partial class DiseaseProtectionComponent : Component
{
    /// <summary>
    /// Пути передачи, на которые действует защита.
    /// </summary>
    [DataField]
    public DiseaseTransmissionRoute Routes = DiseaseTransmissionRoute.Droplet;

    /// <summary>
    /// Множитель шанса заражения. Ноль полностью закрывает указанные пути.
    /// </summary>
    [DataField]
    public float Coefficient = 0.5f;

    /// <summary>
    /// От каких видов контакта защищает снаряжение, если в <see cref="Routes"/> есть контактный путь.
    /// </summary>
    /// <remarks>
    /// Перчатки закрывают только руки, скафандр — от укусов и наростов под ногами.
    /// На остальные пути передачи не влияет.
    /// </remarks>
    [DataField]
    public DiseaseContactKind ContactKinds = DiseaseContactKind.All;
}
