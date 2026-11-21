using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared.Trigger;
using Content.Shared.Storage.Components;
using Content.Shared.Labels.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.Disease.Systems;

/// <summary>
/// Выбрасывает заразный аэрозоль при срабатывании устройства.
/// </summary>
/// <remarks>
/// Облака — обычные сущности со временем жизни, поэтому убирать их не нужно,
/// а заражение считает <see cref="DiseaseCloudSystem"/>.
/// </remarks>
public sealed partial class DiseaseCloudOnTriggerSystem : EntitySystem
{
    [Dependency] private DiseaseRegistrySystem _registry = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private LabelSystem _label = default!;

    /// <summary>
    /// Находка, которая заражает и касанием, делит один штамм между облаком и поверхностью.
    /// </summary>
    /// <remarks>
    /// Иначе от одного ящика пошли бы две разные болезни, и закрывать их пришлось бы разными вакцинами.
    /// Штамм выбирается сразу при появлении, чтобы касание до вскрытия заражало тем же штаммом.
    /// </remarks>
    [SubscribeLocalEvent]
    private void OnMapInit(Entity<DiseaseCloudOnTriggerComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<DiseaseSourceComponent>(ent, out var source) || GetStrain(ent) is not { } strain)
            return;

        source.Templates = new List<ProtoId<DiseasePrototype>>();
        source.Diseases = new List<EntityUid> { strain };
    }

    [SubscribeLocalEvent]
    private void OnTrigger(Entity<DiseaseCloudOnTriggerComponent> ent, ref TriggerEvent args)
    {
        if (args.Key != null && !ent.Comp.KeysIn.Contains(args.Key))
            return;

        if (TryRelease(ent, args.User))
            args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnStorageOpened(Entity<DiseaseCloudOnTriggerComponent> ent, ref StorageAfterOpenEvent args)
    {
        if (ent.Comp.ReleaseOnOpen)
            TryRelease(ent, args.User);
    }

    /// <summary>Выпускает общий штамм в настроенные облака.</summary>
    public bool TryRelease(Entity<DiseaseCloudOnTriggerComponent> ent, EntityUid? user = null)
    {
        if (!CanRelease(ent, user))
            return false;

        if (GetStrain(ent) is not { } disease)
            return false;

        ent.Comp.Released = true;
        DoRelease(ent, ent.Comp.TargetUser ? user!.Value : ent.Owner, disease);
        return true;
    }

    /// <summary>Проверяет, что у источника есть штамм-кандидат и он ещё не израсходован.</summary>
    public bool CanRelease(Entity<DiseaseCloudOnTriggerComponent> ent, EntityUid? user = null)
    {
        return !TerminatingOrDeleted(ent) && (!ent.Comp.SingleUse || !ent.Comp.Released) &&
               ent.Comp.Diseases.Count > 0 && ent.Comp.Clouds > 0 && (!ent.Comp.TargetUser || user != null);
    }

    private void DoRelease(Entity<DiseaseCloudOnTriggerComponent> ent, EntityUid target, EntityUid disease)
    {
        var origin = _transform.GetMapCoordinates(target);
        for (var i = 0; i < ent.Comp.Clouds; i++)
        {
            // Облака расходятся от точки срабатывания, а не висят одной кучей:
            // иначе достаточно отойти на шаг, и выброс уже никого не задевает.
            var offset = _random.NextVector2(ent.Comp.SpreadRange);
            var cloud = Spawn(ent.Comp.CloudPrototype, origin.Offset(offset));
            EnsureComp<DiseaseCloudComponent>(cloud).Disease = disease;
        }

        if (ent.Comp.SamplePrototype is { } prototype && _registry.TryGetData(disease, out var definition))
        {
            // Находка оставляет пробирку именно выпущенного штамма, чтобы её можно было исследовать без больного.
            var uid = SpawnNextToOrDrop(prototype, ent.Owner);
            var sample = EnsureComp<DiseaseStrainSampleComponent>(uid);
            sample.Disease = disease;
            sample.HasStrain = true;
            sample.IsBlank = false;
            sample.IsDesigned = definition.Designed;
            sample.StrainCode = definition.StrainCode;
            Dirty(uid, sample);
            _label.Label(uid, sample.StrainCode);
        }
    }

    /// <summary>
    /// Штамм устройства: выбирается при первом срабатывании и дальше не меняется.
    /// </summary>
    private EntityUid? GetStrain(Entity<DiseaseCloudOnTriggerComponent> ent)
    {
        if (ent.Comp.Strain is { } existing && _registry.StrainExists(existing))
            return existing;

        var template = _random.Pick(ent.Comp.Diseases);
        ent.Comp.Strain = _registry.CreateStrain(template);
        return ent.Comp.Strain;
    }
}
