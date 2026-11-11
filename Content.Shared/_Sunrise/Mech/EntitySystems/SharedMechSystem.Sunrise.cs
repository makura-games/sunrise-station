using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Whitelist;

#pragma warning disable IDE0130
namespace Content.Shared.Mech.EntitySystems;

public abstract partial class SharedMechSystem
{
    [Dependency] private SharedPointLightSystem _pointLight = default!;
    [Dependency] private AccessReaderSystem _sunriseAccessReader = default!;

    private bool CanSunriseEnter(Entity<MechComponent> ent, EntityUid user)
    {
        if (_whitelistSystem.IsWhitelistPass(ent.Comp.PilotBlacklist, user))
        {
            _popup.PopupEntity(Loc.GetString("mech-no-enter", ("item", ent.Owner)), user);
            return false;
        }

        if (TryComp<AccessReaderComponent>(ent, out var accessReader) &&
            !_sunriseAccessReader.IsAllowed(user, ent, accessReader))
        {
            _popup.PopupEntity(Loc.GetString("mech-no-access", ("item", ent.Owner)), user);
            return false;
        }

        return true;
    }

    [SubscribeLocalEvent]
    private void OnToggleLightsEvent(Entity<MechComponent> ent, ref MechToggleLightsEvent args)
    {
        if (args.Handled)
            return;

        ToggleLights(ent);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnSunriseEmagged(Entity<MechComponent> ent, ref GotEmaggedEvent args)
    {
        if (!ent.Comp.BreakOnEmag)
            return;

        args.Handled = true;
        ent.Comp.EquipmentWhitelist = null;
        Dirty(ent);
    }

    public void ToggleLights(Entity<MechComponent> ent)
    {
        if (!_pointLight.TryGetLight(ent, out var pointLight))
            return;

        ent.Comp.Lights = !ent.Comp.Lights;
        _pointLight.SetEnabled(ent, ent.Comp.Lights, pointLight);
        _actions.SetToggled(ent.Comp.MechLightsActionEntity, ent.Comp.Lights);
        var mechSayEvent = new MechSayEvent(ent, ent.Comp.Lights
            ? ent.Comp.MessageEnableLight
            : ent.Comp.MessageDisableLight);
        RaiseLocalEvent(ent, ref mechSayEvent, true);
        Dirty(ent);
    }
}
