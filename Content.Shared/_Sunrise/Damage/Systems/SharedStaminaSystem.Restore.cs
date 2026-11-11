using Content.Shared.Damage.Components;

namespace Content.Shared.Damage.Systems;

// Методы восстановления выносливости.
public abstract partial class SharedStaminaSystem
{
    /// <summary>
    /// Восстанавливает выносливость.
    /// </summary>
    public void RestoreStamina(Entity<StaminaComponent?> ent, float amount)
    {
        if (amount <= 0f || !Resolve(ent, ref ent.Comp, false))
            return;

        if (ent.Comp.Critical)
            ExitStamCrit(ent, ent.Comp);

        TakeStaminaDamage(
            ent,
            -amount,
            ent.Comp,
            visual: false,
            ignoreResist: true);
    }
}
