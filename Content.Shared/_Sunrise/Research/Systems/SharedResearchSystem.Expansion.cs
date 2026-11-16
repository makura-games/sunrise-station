using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Research.Systems;

public abstract partial class SharedResearchSystem
{
    public bool IsTechnologyWithinTierLimit(
        TechnologyDatabaseComponent component,
        TechnologyPrototype technology)
    {
        return component.MaximumTechnologyTier is not { } maximumTier || technology.Tier <= maximumTier;
    }

    public int GetDisciplineLockoutTier(TechnologyDatabaseComponent component, TechDisciplinePrototype discipline)
    {
        return component.DisciplineLockoutTier ?? discipline.LockoutTier;
    }

    public bool IsTechnologyWithinDisciplineLimit(TechnologyDatabaseComponent component, TechnologyPrototype technology)
    {
        if (component.DisciplineLockoutTier is not { } lockoutTier || technology.Tier < lockoutTier)
            return true;

        return IsResearchDisciplineSelected(component, technology.Discipline) ||
               GetSelectedDisciplines(component).Count < component.ResearchDisciplineLimit;
    }

    public List<ProtoId<TechDisciplinePrototype>> GetSelectedDisciplines(TechnologyDatabaseComponent component)
    {
        var disciplines = new List<ProtoId<TechDisciplinePrototype>>();
        if (component.MainDiscipline is { } mainDiscipline)
            disciplines.Add(mainDiscipline);

        foreach (var discipline in component.AdditionalDisciplines)
        {
            if (!disciplines.Contains(discipline))
                disciplines.Add(discipline);
        }

        return disciplines;
    }

    public bool HasCompletedResearch(TechnologyDatabaseComponent component)
    {
        var selectedDisciplines = GetSelectedDisciplines(component);
        if (selectedDisciplines.Count != component.ResearchDisciplineLimit)
            return false;

        foreach (var technology in ProtoMan.EnumeratePrototypes<TechnologyPrototype>())
        {
            if (technology.Hidden || technology.RequiresResearchCompletion)
                continue;

            if (!component.SupportedDisciplines.Contains(technology.Discipline))
                continue;

            var discipline = ProtoMan.Index(technology.Discipline);
            if (technology.Tier >= GetDisciplineLockoutTier(component, discipline) && !selectedDisciplines.Contains(technology.Discipline))
                continue;

            if (!component.UnlockedTechnologies.Contains(technology.ID))
                return false;
        }

        return true;
    }

    public void AddResearchDisciplineSlot(Entity<TechnologyDatabaseComponent> entity, int amount)
    {
        if (amount <= 0)
            return;

        entity.Comp.ResearchDisciplineLimit += amount;
        Dirty(entity);
    }

    private bool IsResearchDisciplineSelected(
        TechnologyDatabaseComponent component,
        ProtoId<TechDisciplinePrototype> discipline)
    {
        return component.MainDiscipline == discipline || component.AdditionalDisciplines.Contains(discipline);
    }

    private void ResetResearchExpansion(TechnologyDatabaseComponent component)
    {
        component.MainDiscipline = null;
        component.AdditionalDisciplines.Clear();
        component.ResearchDisciplineLimit = 1;
    }
}
