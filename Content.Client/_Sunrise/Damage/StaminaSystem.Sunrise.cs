using Content.Shared.Damage.Components;
using Robust.Client.GameObjects;

#pragma warning disable IDE0130
namespace Content.Client.Damage.Systems;

public sealed partial class StaminaSystem
{
    private void PlayAnimation(EntityUid uid)
    {
        if (TryComp<StaminaComponent>(uid, out var stamina) && TryComp<SpriteComponent>(uid, out var sprite))
            PlayAnimation((uid, stamina, sprite));
    }
}
