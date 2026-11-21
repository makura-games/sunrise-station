namespace Content.Server._Sunrise.Disease.Components;

/// <summary>Синтезатор с выбранной операцией, для которого обновляется дрейф смеси.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ActiveDiseaseMixComponent : Component
{
    /// <summary>Следующее обновление сетевых показателей смеси.</summary>
    [ViewVariables, AutoPausedField]
    public TimeSpan NextUpdate;
}
