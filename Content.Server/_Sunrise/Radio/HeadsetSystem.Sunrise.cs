using Content.Shared.Radio.Components;
using Content.Shared._Sunrise.Radio;
using Content.Shared.Interaction;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

// ReSharper disable once CheckNamespace
namespace Content.Server.Radio.EntitySystems;

public sealed partial class HeadsetSystem
{
    private void OnActivate(Entity<HeadsetComponent> ent, ref ActivateInWorldEvent args)
    {
        if (TryComp<ActorComponent>(args.User, out var actor))
        {
            _ui.OpenUi(ent.Owner, HeadsetUiKey.Key, actor.PlayerSession);
            args.Handled = true;
        }
    }
}
