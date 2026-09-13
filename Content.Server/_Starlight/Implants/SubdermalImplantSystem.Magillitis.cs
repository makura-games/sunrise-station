using Content.Server.Polymorph.Systems;
using Content.Shared.Implants.Components;
using Content.Shared.Popups;
using Content.Shared.Zombies;
using Robust.Shared.Player;

#pragma warning disable IDE0130 // Пространство имён соответствует расширяемой системе.
namespace Content.Server.Implants;

public sealed partial class SubdermalImplantSystem
{
    [Dependency] private PolymorphSystem _polymorph = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private void InitializeStarlight()
    {
        SubscribeLocalEvent<SubdermalImplantComponent, UseMagillitisSerumImplantEvent>(OnMagillitisSerumImplant);
    }

    private void OnMagillitisSerumImplant(
        Entity<SubdermalImplantComponent> ent,
        ref UseMagillitisSerumImplantEvent args)
    {
        if (ent.Comp.ImplantedEntity is not { } implanted || HasComp<ZombieComponent>(implanted))
            return;

        if (_polymorph.PolymorphEntity(implanted, "RampagingGorilla") is not { } polymorphed)
            return;

        _popup.PopupEntity(
            Loc.GetString("magillitisserum-implant-activated-others", ("entity", polymorphed)),
            polymorphed,
            Filter.PvsExcept(polymorphed),
            true);
        _popup.PopupEntity(
            Loc.GetString("magillitisserum-implant-activated-user"),
            polymorphed,
            polymorphed);

        args.Handled = true;
        QueueDel(ent);
    }
}
