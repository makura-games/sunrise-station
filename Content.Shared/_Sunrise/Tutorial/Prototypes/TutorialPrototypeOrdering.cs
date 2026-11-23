using System;
using System.Collections.Generic;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.Prototypes;

public static class TutorialPrototypeOrdering
{
    public static List<TutorialSequencePrototype> GetOrderedTutorials(
        TutorialCategoryPrototype category,
        IPrototypeManager prototype)
    {
        var tutorials = new List<(int Index, TutorialSequencePrototype Tutorial)>();

        for (var i = 0; i < category.Tutorials.Count; i++)
        {
            if (!prototype.TryIndex(category.Tutorials[i], out var tutorial))
                continue;

            tutorials.Add((i, tutorial));
        }

        tutorials.Sort(CompareTutorials);

        var result = new List<TutorialSequencePrototype>(tutorials.Count);
        foreach (var (_, tutorial) in tutorials)
        {
            result.Add(tutorial);
        }

        return result;
    }

    public static bool TryGetNextTutorial(
        IPrototypeManager prototype,
        ProtoId<TutorialSequencePrototype> currentId,
        out TutorialSequencePrototype? nextTutorial)
    {
        nextTutorial = null;
        var categories = new List<TutorialCategoryPrototype>(prototype.EnumeratePrototypes<TutorialCategoryPrototype>());
        categories.Sort((a, b) => string.Compare(a.ID, b.ID, StringComparison.Ordinal));

        foreach (var category in categories)
        {
            var tutorials = GetOrderedTutorials(category, prototype);

            for (var i = 0; i < tutorials.Count; i++)
            {
                if (tutorials[i].ID != currentId.Id)
                    continue;

                if (i + 1 >= tutorials.Count)
                    return false;

                nextTutorial = tutorials[i + 1];
                return true;
            }
        }

        return false;
    }

    private static int CompareTutorials(
        (int Index, TutorialSequencePrototype Tutorial) left,
        (int Index, TutorialSequencePrototype Tutorial) right)
    {
        var priority = left.Tutorial.Priority.CompareTo(right.Tutorial.Priority);
        if (priority != 0)
            return priority;

        return left.Index.CompareTo(right.Index);
    }
}
