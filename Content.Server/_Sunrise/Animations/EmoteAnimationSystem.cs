using System.Reflection.Metadata;
using Content.Server.Chat.Systems;
using Content.Shared._Sunrise.Animations;
using Content.Shared._Sunrise.Flip;
using Content.Shared._Sunrise.Jump;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Emoting;
using Content.Shared.Gravity;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Animations;

public sealed partial class EmoteAnimationSystem : EntitySystem
{
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private SharedJumpSystem _jumpSystem = default!;
    [Dependency] private SharedFlipSystem _flipSystem = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private DamageableSystem _damageableSystem = default!;

    private static readonly ProtoId<DamageTypePrototype> BluntDamageType = "Blunt";

    public override void Initialize()
    {
        SubscribeLocalEvent<EmoteAnimationComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<EmoteAnimationComponent, EmoteEvent>(OnEmote);
    }

    private void OnGetState(Entity<EmoteAnimationComponent> ent, ref ComponentGetState args)
    {
        args.State = new EmoteAnimationComponent.EmoteAnimationComponentState(ent.Comp.AnimationId);
    }

    private void OnEmote(EntityUid uid, EmoteAnimationComponent component, ref EmoteEvent args)
    {
        if (args.Handled || !args.Emote.Category.HasFlag(EmoteCategory.Verb))
            return;

        PlayEmoteAnimation(uid, component, args.Emote.ID);
    }

    public void PlayEmoteAnimation(EntityUid uid, EmoteAnimationComponent component, string emoteId)
    {
        if (emoteId == "Lay")
        {
            if (_gravity.IsWeightless(uid))
                return;

            if (_standing.IsDown(uid))
                _stun.TryStanding(uid);
            else
                _stun.TryKnockdown(uid, TimeSpan.FromSeconds(0.5), true, false, false);

            return;
        }

        if (emoteId == "Jump")
        {
            _jumpSystem.TryJump(uid);
        }

        if (emoteId == "Flip")
        {
            _flipSystem.TryFlip(uid);
        }

        if (emoteId == "FallOnNeck")
        {
            var damage = new DamageSpecifier(_prototypeManager.Index(BluntDamageType), 100);
            _damageableSystem.ChangeDamage(uid, damage, true, ignoreVariance: true, ignoreGlobalModifiers: true);
        }

        component.AnimationId = emoteId;
        Dirty(uid, component);
    }
}
