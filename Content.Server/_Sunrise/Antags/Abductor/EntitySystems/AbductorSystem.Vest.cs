using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Clothing.Components;
using Content.Shared._Sunrise.Antags.Abductor;
using Content.Shared.Inventory.Events;
using Content.Shared.Stealth.Components;
using Content.Shared.Mobs.Components;
using System;
using Content.Shared.ActionBlocker;
using System.Linq;
using Content.Shared.Popups;
using Content.Shared.Interaction;
using Content.Shared.Toggleable;

namespace Content.Server._Sunrise.Antags.Abductor;

public sealed partial class AbductorSystem : SharedAbductorSystem
{
    [Dependency] private ClothingSystem _clothing = default!;
    public void InitializeVest()
    {
        SubscribeLocalEvent<AbductorVestComponent, AfterInteractEvent>(OnVestInteract);
        SubscribeLocalEvent<AbductorVestComponent, ItemSwitchedEvent>(OnItemSwitch);
        SubscribeLocalEvent<AbductorVestComponent, ToggleActionEvent>(OnToggle);
    }

    private void OnToggle(Entity<AbductorVestComponent> ent, ref ToggleActionEvent args)
    {
        if (ent.Comp.CurrentState == AbductorArmorModeType.Combat)
            _popup.PopupEntity(Loc.GetString("need-switch-mode"), ent.Owner, args.Performer, PopupType.MediumCaution);
    }
    private void OnItemSwitch(EntityUid uid, AbductorVestComponent component, ref ItemSwitchedEvent args)
    {

        if (Enum.TryParse<AbductorArmorModeType>(args.State, ignoreCase: true, out var State))
            component.CurrentState = State;

        var user = Transform(uid).ParentUid;

        if (State == AbductorArmorModeType.Combat)
        {
            if (TryComp<ClothingComponent>(uid, out var clothingComponent))
                _clothing.SetEquippedPrefix(uid, "combat", clothingComponent);

            if (HasComp<MobStateComponent>(user) && HasComp<StealthComponent>(user))
            {
                RemComp<StealthComponent>(user);
                RemComp<StealthOnMoveComponent>(user);
            }
        }
    }

    private void OnVestInteract(Entity<AbductorVestComponent> ent, ref AfterInteractEvent args)
    {
        if (!_actionBlockerSystem.CanInstrumentInteract(args.User, args.Used, args.Target)) return;
        if (!args.Target.HasValue) return;

        if (TryComp<AbductorConsoleComponent>(args.Target, out var console))
        {
            var netEntity = GetNetEntity(ent);
            console.Armor = netEntity;
            _popup.PopupEntity(Loc.GetString("abductors-ui-vest-linked"), args.User);
            UpdateGui(netEntity, (args.Target.Value, console));
        }
    }
}
