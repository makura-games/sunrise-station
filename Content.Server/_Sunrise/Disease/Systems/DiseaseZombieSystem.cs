using Content.Server._Sunrise.Disease.Components;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared._Sunrise.Disease.Events;
using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Zombies;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Disease.Systems;

/// <summary>
/// Связывает зомби-заражение с вирусологией: укушенный и обратившийся становятся носителями штамма,
/// который можно взять мазком, изучить и закрыть вакциной.
/// </summary>
/// <remarks>
/// Вакцина работает только как профилактика: она спасает укушенного, пока тот не обратился,
/// и защищает здорового. Обратившегося она не возвращает — это задача не медотдела.
/// </remarks>
public sealed partial class DiseaseZombieSystem : EntitySystem
{
    [Dependency] private DiseaseSystem _disease = default!;
    [Dependency] private DiseaseRegistrySystem _registry = default!;

    /// <summary>
    /// Архетип, под которым зомби-зараза попадает в вирусологию.
    /// </summary>
    private static readonly ProtoId<DiseasePrototype> ZombieVirus = "SunriseZombieVirus";

    /// <summary>
    /// Укушенный становится носителем: теперь с него можно взять мазок и завести дело.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnPendingZombie(Entity<PendingZombieComponent> ent, ref ComponentStartup args)
    {
        // Привитому заражение не грозит: вакцина уже стоит.
        if (HasComp<ZombieImmuneComponent>(ent))
            return;

        if (GetStrain(ent.Owner) is { } virus)
            _disease.Infect(ent.Owner, virus);
    }

    /// <summary>
    /// Обратившийся тоже остаётся носителем: с трупа или пойманного зомби берут образец штамма.
    /// </summary>
    /// <remarks>
    /// Ловится MapInit, а не запуск компонента: пара «компонент + событие» в шине уникальна на весь
    /// проект, и ZombieComponent с ComponentStartup уже занят способностями зомби. Вдобавок MapInit
    /// ловит зомби, заспавненных прототипом, а не обратившихся по ходу раунда.
    /// </remarks>
    [SubscribeLocalEvent]
    private void OnZombie(Entity<ZombieComponent> ent, ref MapInitEvent args)
    {
        if (GetStrain(ent.Owner) is { } virus)
            _disease.Infect(ent.Owner, virus);
    }

    /// <summary>
    /// Определяет, каким штаммом болен этот зомби.
    /// </summary>
    /// <remarks>
    /// Порядок проверок задаёт и саму механику: штамм идёт по цепочке укусов от того, кто укусил,
    /// нулевые пациенты берут его у своей вспышки, а всё прочее — зомби из меню спавна и прочие
    /// одиночки — делит природный штамм архетипа, общий на весь раунд.
    /// </remarks>
    private EntityUid? GetStrain(EntityUid target)
    {
        // Унаследованный штамм нужен один раз, поэтому метку снимаем сразу.
        if (TryComp<DiseaseZombieSourceComponent>(target, out var source))
        {
            RemCompDeferred<DiseaseZombieSourceComponent>(target);
            if (GetCarriedStrain(source.Source) is { } inherited)
                return inherited;
        }

        // Уже носитель: повторное обращение не должно заводить второй штамм.
        if (GetCarriedStrain(target) is { } current)
            return current;

        // Нулевой пациент вспышки: штамм общий на всё правило и заводится при первом заражении.
        if (HasComp<IncurableZombieComponent>(target) && FindOutbreak() is { } outbreak)
        {
            if (outbreak.Comp.Strain is { } existing && _registry.StrainExists(existing))
                return existing;

            var created = _registry.CreateStrain(ZombieVirus);
            outbreak.Comp.Strain = created;
            return created;
        }

        // Одиночкам достаётся природный штамм: реестр хранит его сам, по одному на архетип за раунд.
        return _registry.GetNatural(ZombieVirus);
    }

    /// <summary>Зомби-штамм, которым уже болеет носитель, если он есть.</summary>
    private EntityUid? GetCarriedStrain(EntityUid carrier)
    {
        if (TerminatingOrDeleted(carrier))
            return null;

        foreach (var instance in _disease.GetDiseases(carrier))
        {
            if (_registry.TryGetData(instance.Disease, out var data) && data.Template == ZombieVirus)
                return instance.Disease;
        }

        return null;
    }

    /// <summary>
    /// Идущая зомби-вспышка, на которой лежит её штамм.
    /// </summary>
    /// <remarks>
    /// Правило ищется по факту, а не запоминается при старте: порядок между выбором антагонистов
    /// и запуском правила не закреплён, и привязка «по последнему старту» разъезжалась бы.
    /// </remarks>
    private Entity<DiseaseZombieStrainComponent>? FindOutbreak()
    {
        var query = EntityQueryEnumerator<ZombieRuleComponent, ActiveGameRuleComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            return (uid, EnsureComp<DiseaseZombieStrainComponent>(uid));
        }

        return null;
    }

    /// <summary>
    /// Прививка отменяет обращение и закрывает человека от повторного укуса.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnVaccinated(Entity<MobStateComponent> ent, ref DiseaseVaccinatedEvent args)
    {
        if (!_registry.TryGetData(args.Disease, out var data) || data.Template != ZombieVirus)
            return;

        // Уже обратившемуся прививка не поможет: лечения обращения в вирусологии нет.
        if (HasComp<ZombieComponent>(ent))
            return;

        RemComp<PendingZombieComponent>(ent);
        EnsureComp<ZombieImmuneComponent>(ent);
    }
}
