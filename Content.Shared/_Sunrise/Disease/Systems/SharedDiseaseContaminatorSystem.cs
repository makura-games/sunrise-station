using Content.Shared._Sunrise.Disease.Components;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Whitelist;

namespace Content.Shared._Sunrise.Disease.Systems;

/// <summary>
/// Подмешивание культуры в еду: старт do-after, расход порции и сообщения предсказываются на клиенте.
/// </summary>
/// <remarks>
/// Сам штамм клиенту неизвестен, поэтому превращение еды в очаг делает сервер в <see cref="OnContaminated"/>.
/// Полоса прогресса скрыта от окружающих: подмешивание — диверсия, а не открытое действие.
/// </remarks>
public abstract partial class SharedDiseaseContaminatorSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private IngestionSystem _ingestion = default!;
    [Dependency] private OpenableSystem _openable = default!;
    [Dependency] private EntityQuery<EdibleComponent> _edibleQuery = default!;
    [Dependency] private EntityQuery<DiseaseStrainSampleComponent> _sampleQuery = default!;

    [SubscribeLocalEvent]
    private void OnAfterInteract(Entity<DiseaseContaminatorComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        // Пробирка по-прежнему засевает чашки: всё, что не еда, обрабатывают другие системы.
        if (!_whitelist.IsWhitelistPass(ent.Comp.Whitelist, target))
            return;

        TryStartContamination(ent, args.User, target);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnDoAfter(Entity<DiseaseContaminatorComponent> ent, ref DiseaseContaminateDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        if (TryContaminate(ent, args.User, target))
            args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnExamined(Entity<DiseaseContaminatorComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("disease-contaminator-examine-uses", ("uses", ent.Comp.Uses)));
    }

    /// <summary>Начинает добавление дозы в открытую и непустую еду или напиток.</summary>
    public bool TryStartContamination(Entity<DiseaseContaminatorComponent> ent, EntityUid user, EntityUid target)
    {
        if (!CanContaminate(ent, user, target, quiet: false))
            return false;

        return _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.Delay,
            new DiseaseContaminateDoAfterEvent(), ent, target: target, used: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            Hidden = true,
        });
    }

    /// <summary>Проверяет завершённое действие и расходует дозу после успешного заражения.</summary>
    public bool TryContaminate(Entity<DiseaseContaminatorComponent> ent, EntityUid user, EntityUid target)
    {
        if (!CanContaminate(ent, user, target) || !OnContaminated(ent, user, target))
            return false;

        ent.Comp.Uses--;
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString("disease-contaminator-popup-done", ("target", target)), target, user);
        return true;
    }

    /// <summary>Проверяет остаток доз, содержимое пробирки и доступность съедобного раствора.</summary>
    public bool CanContaminate(Entity<DiseaseContaminatorComponent> ent, EntityUid user, EntityUid target, bool quiet = true)
    {
        if (TerminatingOrDeleted(ent) || TerminatingOrDeleted(target) || target == ent.Owner ||
            HasComp<MobStateComponent>(target) || !_whitelist.IsWhitelistPass(ent.Comp.Whitelist, target) ||
            !_edibleQuery.TryComp(target, out var edible))
        {
            return false;
        }

        if (ent.Comp.Uses <= 0 || !ValidateSample(ent))
        {
            if (!quiet)
                _popup.PopupEntity(Loc.GetString("disease-contaminator-popup-empty"), ent, user);
            return false;
        }

        if (_openable.IsClosed(target, quiet ? null : user))
            return false;

        if (!_ingestion.IsEmpty((target, edible)))
            return true;

        if (!quiet)
            _popup.PopupEntity(Loc.GetString("disease-contaminator-popup-no-content"), target, user);
        return false;
    }

    /// <summary>Проверяет публичное состояние мазка; сервер дополнительно проверяет сам штамм.</summary>
    protected virtual bool ValidateSample(Entity<DiseaseContaminatorComponent> ent)
    {
        return _sampleQuery.TryComp(ent, out var sample) && sample.HasStrain;
    }

    /// <summary>Создаёт очаг на сервере, прежде чем общий код израсходует дозу.</summary>
    protected virtual bool OnContaminated(Entity<DiseaseContaminatorComponent> ent, EntityUid user, EntityUid target)
    {
        return true;
    }
}
