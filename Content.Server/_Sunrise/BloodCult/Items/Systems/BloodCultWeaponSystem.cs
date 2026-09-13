using Content.Server._Sunrise.BloodCult.Items.Components;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared._Sunrise.BloodCult.Components;
using Content.Shared._Sunrise.BloodCult.Items;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Utility;

namespace Content.Server._Sunrise.BloodCult.Items.Systems;

public sealed partial class BloodCultWeaponSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private BodySystem _body = default!;
    [Dependency] private DamageableSystem _damageableSystem = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private SharedStunSystem _stunSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodCultWeaponComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<BloodCultWeaponComponent, AfterInteractEvent>(OnInteractEvent);
        SubscribeLocalEvent<BloodBoltComponent, ProjectileHitEvent>(BloodBoltOnCollide);
    }

    private void BloodBoltOnCollide(Entity<BloodBoltComponent> ent, ref ProjectileHitEvent args)
    {
        if (HasComp<ConstructComponent>(args.Target))
        {
            _damageableSystem.TryChangeDamage(args.Target,
                ent.Comp.HealConstruct,
                origin: args.Shooter);
            return;
        }
        if (HasComp<BloodCultistComponent>(args.Target))
        {
            if (_solutionContainer.TryGetInjectableSolution(args.Target, out var injectableSolution, out _))
                _solutionContainer.TryAddReagent(injectableSolution.Value, ent.Comp.UnholyProto, ent.Comp.UnholyVolume, out _);

            return;
        }
        if (HasComp<MobStateComponent>(args.Target))
        {
            _damageableSystem.TryChangeDamage(args.Target,
                ent.Comp.Damage,
                origin: args.Shooter);
        }
    }

    private void OnInteractEvent(EntityUid uid, BloodCultWeaponComponent component, AfterInteractEvent args)
    {
        if (!args.CanReach || args.Handled || args.Target == null)
            return;

        if (!HasComp<BloodCultistComponent>(args.User))
            return;

        if (!TryComp<BloodCultistComponent>(args.Target, out var cultistComponent))
            return;

        if (!TryComp<BodyComponent>(args.Target, out var body))
            return;

        var convert = false;

        if (cultistComponent.HolyConvertToken != null)
        {
            cultistComponent.HolyConvertToken?.Cancel();
            cultistComponent.HolyConvertToken = null;
            convert = true;
        }

        if (_body.TryGetOrganWithComponent<StomachComponent>((args.Target.Value, body), out var stomach))
        {
            if (_solutionContainer.ResolveSolution(stomach.Owner,
                    StomachSystem.DefaultSolutionName,
                    ref stomach.Comp.Solution) &&
                TryConvertHolyWater(stomach.Comp.Solution.Value,
                    component.ConvertedId,
                    component.ConvertedToId))
            {
                convert = true;
            }
        }

        if (_solutionContainer.TryGetInjectableSolution(args.Target.Value, out var injectableSolution, out _))
        {
            if (TryConvertHolyWater(injectableSolution.Value, component.ConvertedId, component.ConvertedToId))
                convert = true;
        }

        if (!convert)
            return;

        _audio.PlayPvs(component.ConvertHolyWaterSound, args.Target.Value);
        _popup.PopupEntity(Loc.GetString("holy-water-deconverted"), args.User, args.User);
    }

    private bool TryConvertHolyWater(Entity<SolutionComponent> solution, string fromReagentId, string toReagentId)
    {
        var contents = solution.Comp.Solution.Contents;

        // Идём с конца: RemoveReagent использует RemoveSwap и меняет порядок списка.
        for (var i = contents.Count - 1; i >= 0; i--)
        {
            var reagent = contents[i];
            if (reagent.Reagent.Prototype != fromReagentId)
                continue;

            var amount = _solutionContainer.RemoveReagent(solution, reagent.Reagent, reagent.Quantity);
            if (amount <= FixedPoint2.Zero)
                continue;

            var convertedReagent = new ReagentId(toReagentId, reagent.Reagent.Data);
            _solutionContainer.TryAddReagent(solution, convertedReagent, amount, out var added);
            if (added < amount)
                _solutionContainer.TryAddReagent(solution, reagent.Reagent, amount - added, out _);

            return added > FixedPoint2.Zero;
        }

        return false;
    }

    private void OnMeleeHit(EntityUid uid, BloodCultWeaponComponent component, MeleeHitEvent args)
    {
        if (HasComp<BloodCultistComponent>(args.User))
            return;

        _stunSystem.TryAddParalyzeDuration(args.User, TimeSpan.FromSeconds(component.StuhTime));
        _damageableSystem.TryChangeDamage(args.User, component.Damage, origin: uid);
    }
}
