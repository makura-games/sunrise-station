using Content.Shared._Sunrise.Humanoid;
using Content.Shared._Sunrise.Antags.Abductor;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Speech.Muting;
using Content.Shared.Starlight.Medical.Surgery.Events;
using Content.Shared.Starlight.Medical.Surgery.Steps.Parts;

namespace Content.Server._Starlight.Medical.Surgery;

public sealed partial class OrganSystem : EntitySystem
{

    [Dependency] private BlindableSystem _blindable = default!;
    [Dependency] private DamageableSystem _damageableSystem = default!;
    [Dependency] private IComponentFactory _compFactory = default!;
    [Dependency] private SunriseHumanoidBodySystem _sunriseBody = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FunctionalOrganComponent, SurgeryOrganImplantationCompleted>(OnFunctionalOrganImplanted);
        SubscribeLocalEvent<FunctionalOrganComponent, SurgeryOrganExtracted>(OnFunctionalOrganExtracted);

        SubscribeLocalEvent<OrganEyesComponent, SurgeryOrganImplantationCompleted>(OnEyeImplanted);
        SubscribeLocalEvent<OrganEyesComponent, SurgeryOrganExtracted>(OnEyeExtracted);

        SubscribeLocalEvent<OrganTongueComponent, SurgeryOrganImplantationCompleted>(OnTongueImplanted);
        SubscribeLocalEvent<OrganTongueComponent, SurgeryOrganExtracted>(OnTongueExtracted);

        SubscribeLocalEvent<DamageableComponent, SurgeryOrganImplantationCompleted>(OnOrganImplanted);
        SubscribeLocalEvent<DamageableComponent, SurgeryOrganExtracted>(OnOrganExtracted);

        SubscribeLocalEvent<OrganVisualizationComponent, SurgeryOrganImplantationCompleted>(OnVisualizationImplanted);
        SubscribeLocalEvent<OrganVisualizationComponent, SurgeryOrganExtracted>(OnVisualizationExtracted);
    }

    //

    private void OnFunctionalOrganImplanted(Entity<FunctionalOrganComponent> ent, ref SurgeryOrganImplantationCompleted args)
    {
        foreach (var comp in (ent.Comp.Components ?? []).Values)
            if (!HasComp(args.Body, comp.Component.GetType()))
                AddComp(args.Body, _compFactory.GetComponent(comp.Component.GetType()));
    }

    private void OnFunctionalOrganExtracted(Entity<FunctionalOrganComponent> ent, ref SurgeryOrganExtracted args)
    {
        foreach (var comp in (ent.Comp.Components ?? []).Values)
            if (HasComp(args.Body, comp.Component.GetType()))
                RemComp(args.Body, _compFactory.GetComponent(comp.Component.GetType()));
    }

    //

    private void OnOrganImplanted(Entity<DamageableComponent> ent, ref SurgeryOrganImplantationCompleted args)
    {
        if (!HasComp<DamageableComponent>(args.Body))
            return;

        var organDamage = _damageableSystem.GetAllDamage(ent.AsNullable());
        var change = _damageableSystem.ChangeDamage(args.Body, organDamage, true, false);
        _damageableSystem.ChangeDamage(ent.Owner, change.Invert(), true, false);
    }
    private void OnOrganExtracted(Entity<DamageableComponent> ent, ref SurgeryOrganExtracted args)
    {
        if (!TryComp<OrganDamageComponent>(ent.Owner, out var damageRule)
            || damageRule.Damage is null
            || !HasComp<DamageableComponent>(args.Body))
            return;

        var change = _damageableSystem.ChangeDamage(args.Body, damageRule.Damage.Invert(), true, false);
        _damageableSystem.ChangeDamage(ent.Owner, change.Invert(), true, false);
    }
    private void OnTongueImplanted(Entity<OrganTongueComponent> ent, ref SurgeryOrganImplantationCompleted args)
    {
        if (HasComp<AbductorComponent>(args.Body) || !ent.Comp.IsMuted) return;
        RemComp<MutedComponent>(args.Body);
    }

    private void OnTongueExtracted(Entity<OrganTongueComponent> ent, ref SurgeryOrganExtracted args)
    {
        ent.Comp.IsMuted = HasComp<MutedComponent>(args.Body);
        AddComp<MutedComponent>(args.Body);
    }

    //

    private void OnEyeExtracted(Entity<OrganEyesComponent> ent, ref SurgeryOrganExtracted args)
    {
        if (!TryComp<BlindableComponent>(args.Body, out var blindable)) return;

        ent.Comp.EyeDamage = blindable.EyeDamage;
        ent.Comp.MinDamage = blindable.MinDamage;
        _blindable.UpdateIsBlind((args.Body, blindable));
    }
    private void OnEyeImplanted(Entity<OrganEyesComponent> ent, ref SurgeryOrganImplantationCompleted args)
    {
        if (!TryComp<BlindableComponent>(args.Body, out var blindable)) return;

        _blindable.SetMinDamage((args.Body, blindable), ent.Comp.MinDamage ?? 0);
        _blindable.AdjustEyeDamage((args.Body, blindable), (ent.Comp.EyeDamage ?? 0) - blindable.MaxDamage);
    }

    //

    private void OnVisualizationExtracted(Entity<OrganVisualizationComponent> ent, ref SurgeryOrganExtracted args)
        => _sunriseBody.SetLayersVisibility(args.Body, [ent.Comp.Layer], false);
    private void OnVisualizationImplanted(Entity<OrganVisualizationComponent> ent, ref SurgeryOrganImplantationCompleted args)
    {
        _sunriseBody.SetLayersVisibility(args.Body, [ent.Comp.Layer], true);
        _sunriseBody.SetBaseLayerData(args.Body, ent.Comp.Layer, ent.Comp.Data);
    }
}
