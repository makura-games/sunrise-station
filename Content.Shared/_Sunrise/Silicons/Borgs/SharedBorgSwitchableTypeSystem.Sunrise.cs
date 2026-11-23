using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.Prototypes;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Shared.Silicons.Borgs;

public abstract partial class SharedBorgSwitchableTypeSystem
{
    /// <summary>
    /// Selects a borg type through the existing borg module setup flow.
    /// </summary>
    public bool TrySelectBorgType(Entity<BorgSwitchableTypeComponent?> ent, ProtoId<BorgTypePrototype> borgType)
    {
        if (!CanSelectBorgType(ent, borgType))
            return false;

        DoSelectBorgType((ent, ent.Comp!), borgType);
        return true;
    }

    /// <summary>
    /// Returns whether the borg type can be selected for a chassis that has not selected a type yet.
    /// </summary>
    public bool CanSelectBorgType(Entity<BorgSwitchableTypeComponent?> ent, ProtoId<BorgTypePrototype> borgType)
    {
        return Resolve(ent, ref ent.Comp, false) &&
               ent.Comp.SelectedBorgType == null &&
               ProtoMan.HasIndex(borgType);
    }

    /// <summary>
    /// Applies a borg type selection through the vanilla module selection flow.
    /// </summary>
    private void DoSelectBorgType(Entity<BorgSwitchableTypeComponent> ent, ProtoId<BorgTypePrototype> borgType)
    {
        SelectBorgModule(ent, borgType);
    }
}
