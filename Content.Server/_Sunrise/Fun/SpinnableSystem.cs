using Content.Shared.Interaction;
using Robust.Shared.Random;
using Content.Shared._Sunrise.Fun;
using Content.Shared.Popups;
using Content.Shared.Hands;
using Content.Shared.Verbs;
using Content.Shared.Ghost;

namespace Content.Server._Sunrise.Fun
{
    public sealed partial class SpinnerSystem : EntitySystem
    {
        [Dependency] private IRobustRandom _random = default!;
        [Dependency] private SharedTransformSystem _xform = default!;
        [Dependency] private SharedPopupSystem _popupSystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<SpinnerComponent, ActivateInWorldEvent>(OnActivateInWorld);
            SubscribeLocalEvent<SpinnerComponent, GotEquippedHandEvent>(OnGotEquippedHand);
            SubscribeLocalEvent<SpinnerComponent, GetVerbsEvent<AlternativeVerb>>(OnAlternativeInteract);

        }


        private void OnAlternativeInteract(Entity<SpinnerComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
        {
            if (CompOrNull<GhostComponent>(args.User) is not null || CompOrNull<TransformComponent>(ent) is null)
                return;

            HandleSpinnerActivation(ent, args.User);
        }

        private void OnGotEquippedHand(Entity<SpinnerComponent> ent, ref GotEquippedHandEvent args)
        {
            ent.Comp.IsSpinning = false;
            ent.Comp.RemainingSeconds = 0f;
            ent.Comp.CurrentDegPerSec = 0f;
            Dirty(ent, ent.Comp);
            return;
        }

        private void OnActivateInWorld(Entity<SpinnerComponent> ent, ref ActivateInWorldEvent args)
        {
            var transformComp = CompOrNull<TransformComponent>(ent);
            if (transformComp is null || !transformComp.Anchored)
                return;
            HandleSpinnerActivation(ent, args.User);
        }

        private void HandleSpinnerActivation(Entity<SpinnerComponent> ent, EntityUid userId)
        {
            var userName = CompOrNull<MetaDataComponent>(userId)?.EntityName;
            if (!ent.Comp.IsSpinning)
            {
                StartSpin(ent, ent.Comp);
                _popupSystem.PopupEntity($"{userName} {Loc.GetString("arrow-spin-start")}", userId);
                return;
            }

            if (ent.Comp.RemainingSeconds > ent.Comp.MaxSpinSeconds)
                return;

            if (ent.Comp.CurrentDegPerSec > ent.Comp.MaxDegPerSec)
                return;

            var seconds = _random.NextFloat(ent.Comp.MinSpinSeconds, ent.Comp.MaxSpinSeconds);
            var degPerSec = _random.NextFloat(ent.Comp.MinDegPerSec, ent.Comp.MaxDegPerSec);

            ent.Comp.RemainingSeconds += seconds;
            ent.Comp.CurrentDegPerSec += degPerSec;
            _popupSystem.PopupEntity($"{userName} {Loc.GetString("arrow-speed-up")}", userId);
            Dirty(ent, ent.Comp);
        }

        private void StartSpin(EntityUid uid, SpinnerComponent comp)
        {
            var seconds = _random.NextFloat(comp.MinSpinSeconds, comp.MaxSpinSeconds);
            var degPerSec = _random.NextFloat(comp.MinDegPerSec, comp.MaxDegPerSec);

            comp.IsSpinning = true;
            comp.RemainingSeconds = seconds;
            comp.CurrentDegPerSec = degPerSec;

            Dirty(uid, comp);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var query = EntityQueryEnumerator<SpinnerComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var comp, out var xform))
            {
                if (!comp.IsSpinning)
                    continue;

                var dt = frameTime;

                var deltaDeg = comp.CurrentDegPerSec * dt;

                var newAngle = xform.LocalRotation.Degrees + deltaDeg;
                _xform.SetLocalRotation(uid, Angle.FromDegrees(newAngle));

                comp.RemainingSeconds -= dt;

                if (comp.RemainingSeconds <= 0f)
                {
                    comp.CurrentDegPerSec *= comp.BrakeFactor;
                    if (MathF.Abs(comp.CurrentDegPerSec) < comp.ForceStopSpeed)
                    {
                        comp.IsSpinning = false;
                        comp.CurrentDegPerSec = 0f;
                        comp.RemainingSeconds = 0f;
                        Dirty(uid, comp);
                        continue;
                    }
                }

                if (comp.RemainingSeconds > 0f && comp.RemainingSeconds < comp.SmoothStopAtSecond)
                    comp.CurrentDegPerSec *= comp.SmoothStopBrakeFactor;

                Dirty(uid, comp);
            }
        }
    }
}
