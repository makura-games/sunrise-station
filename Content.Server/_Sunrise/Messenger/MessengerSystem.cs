using System.Diagnostics.CodeAnalysis;
using Content.Server.CartridgeLoader;
using Content.Server._Sunrise.CartridgeLoader.Cartridges;
using Content.Shared._Sunrise.Messenger;
using Content.Shared.Inventory;
using Content.Shared.PDA;
using Content.Shared.Hands.EntitySystems;
using Robust.Server.GameObjects;

namespace Content.Server._Sunrise.Messenger;

public sealed partial class MessengerSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<OpenMessengerRequestEvent>(OnOpenMessengerRequest);
    }

    private void OnOpenMessengerRequest(OpenMessengerRequestEvent ev, EntitySessionEventArgs args)
    {
        var user = args.SenderSession.AttachedEntity;
        if (user == null)
            return;

        if (!TryFindPda(user.Value, out var pda))
            return;

        if (!_cartridgeLoader.TryGetProgram<MessengerCartridgeComponent>(pda.Value, out var programUid) || programUid is not { } program)
            return;

        _cartridgeLoader.ActivateProgram(pda.Value, program);

        _ui.OpenUi(pda.Value, PdaUiKey.Key, args.SenderSession);
    }

    private bool TryFindPda(EntityUid user, [NotNullWhen(true)] out EntityUid? pda)
    {
        pda = null;

        if (_hands.TryGetActiveItem(user, out var heldItem) && HasComp<PdaComponent>(heldItem))
        {
            pda = heldItem;
            return true;
        }

        if (_inventory.TryGetSlotEntity(user, "id", out var idItem) && HasComp<PdaComponent>(idItem))
        {
            pda = idItem;
            return true;
        }

        foreach (var item in _hands.EnumerateHeld(user))
        {
            if (!HasComp<PdaComponent>(item))
                continue;

            pda = item;
            return true;
        }

        return false;
    }
}
