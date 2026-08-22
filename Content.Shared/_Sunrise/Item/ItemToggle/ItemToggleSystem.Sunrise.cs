using Content.Shared._Sunrise.Biocode;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item.ItemToggle.Components;

namespace Content.Shared.Item.ItemToggle;

public sealed partial class ItemToggleSystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private BiocodeSystem _biocode = default!;

    private void InitializeSunriseItemToggle()
    {
        SubscribeLocalEvent<ItemToggleComponent, GotUnequippedHandEvent>(OnItemToggleHandUnequipped);
    }

    private void OnItemToggleHandUnequipped(Entity<ItemToggleComponent> ent, ref GotUnequippedHandEvent args)
    {
        if (!ent.Comp.Activated || ent.Owner != args.Unequipped || !ent.Comp.DeactivateUnequippedHand)
            return;

        Toggle((ent.Owner, ent.Comp), args.User, predicted: ent.Comp.Predictable);
    }

    private bool CanSunriseUseInHand(Entity<ItemToggleComponent> ent, EntityUid user)
    {
        if (HasComp<ItemComponent>(ent) &&
            TryComp<HandsComponent>(user, out var hands) &&
            (!_hands.TryGetActiveItem((user, hands), out var itemInHand) || itemInHand != ent.Owner))
        {
            return false;
        }

        return CanUseBiocodedItem(ent, user);
    }

    private bool CanSunriseActivateVerb(Entity<ItemToggleComponent> ent, EntityUid user)
    {
        if (!ent.Comp.CanActivateInhand)
            return false;

        return CanSunriseUseInHand(ent, user);
    }

    private bool CanSunriseActivateInWorld(Entity<ItemToggleComponent> ent, EntityUid user)
    {
        if (TryComp<HandsComponent>(user, out var hands) &&
            (!_hands.TryGetActiveItem((user, hands), out var itemInHand) ||
             itemInHand != ent.Owner ||
             !ent.Comp.CanActivateInhand))
        {
            return false;
        }

        return CanUseBiocodedItem(ent, user);
    }

    private bool CanUseBiocodedItem(EntityUid item, EntityUid user)
    {
        return !TryComp<BiocodeComponent>(item, out var biocode) ||
               _biocode.CanUse(user, biocode.Factions);
    }
}
