namespace Content.Shared._Sunrise.Disease.Components;

/// <summary>Позволяет вколоть готовый образец штамма.</summary>
[RegisterComponent]
public sealed partial class DiseaseStrainInjectorComponent : Component
{
    /// <summary>Сколько длится укол образцом.</summary>
    [DataField]
    public TimeSpan InjectDelay = TimeSpan.FromSeconds(10);
}
