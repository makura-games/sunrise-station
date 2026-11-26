using Content.Server._Sunrise.Disease.Systems;
using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared.Mobs.Components;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Disease.Commands;

/// <summary>
/// Проверки для админ команд системы болезней.
/// </summary>
public abstract partial class BaseDiseaseCommand : LocalizedEntityCommands
{
    [Dependency] protected DiseaseSystem Disease = default!;
    [Dependency] protected DiseaseRegistrySystem Registry = default!;
    [Dependency] protected IPrototypeManager Proto = default!;

    /// <summary>
    /// Проверка для сущности и аргументов.
    /// </summary>
    protected bool TryParseEntity(IConsoleShell shell, string arg, out EntityUid uid)
    {
        uid = EntityUid.Invalid;
        if (!NetEntity.TryParse(arg, out var netEntity) ||
            !EntityManager.TryGetEntity(netEntity, out var entity) ||
            !EntityManager.EntityExists(entity))
        {
            shell.WriteError(Loc.GetString("shell-could-not-find-entity-with-uid", ("uid", arg)));
            return false;
        }

        uid = entity.Value;
        return true;
    }

    /// <summary>
    /// Проверяет аргумент с болезнью, которой цель болеет прямо сейчас.
    /// </summary>
    /// <remarks>
    /// Принимает и код штамма (NN-123), и ID прототипа. Если по прототипу, то ищется болезнь,
    /// которая уже есть у цели: штаммов одного прототипа в раунде может быть несколько.
    /// </remarks>
    protected bool TryParseDiseaseOf(IConsoleShell shell, EntityUid target, string arg, out EntityUid disease)
    {
        if (Registry.TryGetByCode(arg, out disease))
            return true;

        if (!Proto.TryIndex<DiseasePrototype>(arg, out var proto))
        {
            shell.WriteError(Loc.GetString("cmd-disease-unknown-disease", ("disease", arg)));
            return false;
        }

        foreach (var instance in Disease.GetDiseases(target))
        {
            if (!Registry.TryGetData(instance.Disease, out var data) || data.Template?.Id != proto.ID)
                continue;

            disease = instance.Disease;
            return true;
        }

        shell.WriteError(Loc.GetString("cmd-disease-not-infected-with", ("disease", proto.ID)));
        return false;
    }

    /// <summary>
    /// Проверяет аргумент с болезнью для заражения.
    /// </summary>
    /// <remarks>
    /// Код штамма берёт существующий штамм, ID прототипа — общий штамм раунда, заводя его при нужде.
    /// </remarks>
    protected bool TryParseStrain(IConsoleShell shell, string arg, out EntityUid strain)
    {
        if (Registry.TryGetByCode(arg, out strain))
            return true;

        if (Proto.TryIndex<DiseasePrototype>(arg, out var proto) &&
            Registry.GetNatural(proto.ID) is { } natural)
        {
            strain = natural;
            return true;
        }

        shell.WriteError(Loc.GetString("cmd-disease-unknown-disease", ("disease", arg)));
        return false;
    }

    /// <summary>
    /// Какое название получит болезнь в ответе на команду: кодом штамма и, если он есть, прототипом.
    /// </summary>
    protected string DescribeDisease(EntityUid disease)
    {
        if (!Registry.TryGetData(disease, out var data))
            return disease.ToString();

        return data.Template is { } template ? $"{template.Id} [{data.StrainCode}]" : data.StrainCode;
    }

    /// <summary>
    /// Автодополнение для аргумента с сущностью.
    /// </summary>
    protected CompletionResult EntityCompletion(string arg)
    {
        return CompletionResult.FromHintOptions(
            CompletionHelper.Components<MobStateComponent>(arg, EntityManager),
            Loc.GetString("shell-argument-uid"));
    }

    /// <summary>
    /// Автодополнение для аргумента с болезнью: сначала живые штаммы раунда, затем прототипы.
    /// </summary>
    protected CompletionResult DiseaseCompletion()
    {
        var options = new List<CompletionOption>();
        foreach (var disease in Registry.GetDiseases())
        {
            if (Registry.TryGetData(disease, out var data))
                options.Add(new CompletionOption(data.StrainCode, data.Template?.Id));
        }

        options.AddRange(CompletionHelper.PrototypeIDs<DiseasePrototype>(proto: Proto));
        return CompletionResult.FromHintOptions(options, Loc.GetString("cmd-disease-hint-disease"));
    }
}
