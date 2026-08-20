namespace Content.Server._Sunrise.Electropack;

/// <summary>
/// Маркер: запускает триггер при включении боевого режима (<see cref="Content.Shared.Trigger.Systems.TriggerSystem"/>).
/// Добавляется на рюкзак-электрошокер (<c>ClothingBackpackElectropack</c>).
/// </summary>
[RegisterComponent]
public sealed partial class TriggerOnCombatModeEnabledComponent : Component
{
    /// <summary>
    /// Если true, боевой режим отключается после срабатывания триггера.
    /// </summary>
    [DataField]
    public bool DisableCombatModeAfterTrigger = true;
}
