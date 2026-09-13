using System.Linq;
using Content.Shared._Sunrise.Laws;
using Content.Shared._Sunrise.Laws.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.CriminalRecords.Systems;

public abstract partial class SharedSunriseCriminalRecordsSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private SharedStationCorporateLawSystem _corporateLawSystem = default!;

    private static readonly Dictionary<int, (int Min, int Max)> CategoryRanges = new()
    {
        [1] = (5, 10),
        [2] = (10, 15),
        [3] = (15, 25),
        [4] = (20, 35),
        [5] = (30, 50),
    };

    public int CalculateSentence(CriminalCase @case, List<CriminalCase> allCases)
    {
        _corporateLawSystem.GetEffectiveLawset(@case.OriginStation, out var provisions, out var articles, out var circumstances, out var threshold);

        var sentence = CalculateSentenceInternal(@case, allCases, provisions, circumstances, articles, threshold, out var isWarning);
        @case.IsWarning = isWarning;
        return sentence;
    }

    private int CalculateSentenceInternal(
        CriminalCase @case,
        List<CriminalCase> allCases,
        List<ProtoId<CorporateLawPrototype>> provisions,
        List<ProtoId<CorporateLawPrototype>> circumstances,
        List<ProtoId<CorporateLawSectionPrototype>> articles,
        int threshold,
        out bool isWarning)
    {
        isWarning = false;
        @case.SentenceBreakdown ??= new List<SentenceBreakdownEntry>();
        @case.SentenceBreakdown.Clear();

        if (@case.Laws.Count == 0)
            return 0;

        var lawProtos = new List<CorporateLawPrototype>();
        foreach (var id in @case.Laws)
        {
            if (_prototypeManager.TryIndex(id, out var law))
                lawProtos.Add(law);
        }

        // --- Grouping by "line" (ArtCode % 100) ---
        // We include only valid numeric laws, but the most severe category for each "line" wins.
        var effectiveCharges = lawProtos
            .Select(l => (Law: l, Code: int.TryParse(l.LawIdentifier, out var c) ? (int?) c : null))
            .Where(x => x.Code != null)
            .GroupBy(x => x.Code!.Value % 100)
            .Select(group => group
                .OrderByDescending(x => x.Code!.Value / 100)
                .First().Law)
            .ToList();

        // --- WARNING SYSTEM: 1xx and 2xx blocks ---
        var finalEffectiveCharges = new List<CorporateLawPrototype>();
        foreach (var law in effectiveCharges)
        {
            if (int.TryParse(law.LawIdentifier, out int code))
            {
                int cat = code / 100;
                if (cat == 1 || cat == 2)
                {
                    char block = law.LawIdentifier![0];
                    bool blockViolatedBefore = allCases
                        .Where(c => c.Id != @case.Id && (c.Status == CriminalCaseStatus.Finished || c.Status == CriminalCaseStatus.Incarcerated || c.Status == CriminalCaseStatus.Closed))
                        .SelectMany(c => c.Laws)
                        .Any(l => _prototypeManager.TryIndex(l, out var p) && p.LawIdentifier != null && p.LawIdentifier.Length > 0 && p.LawIdentifier[0] == block);

                    if (!blockViolatedBefore)
                    {
                        isWarning = true;
                        @case.SentenceBreakdown.Add(new SentenceBreakdownEntry("sunrise-records-breakdown-warning", ("id", law.LawIdentifier!)));
                        continue;
                    }
                }
            }
            finalEffectiveCharges.Add(law);
        }

        if (finalEffectiveCharges.Count == 0)
            return 0;

        // --- Calculation ---
        var normalCharges = finalEffectiveCharges.Where(l => (int.TryParse(l.LawIdentifier, out int c) ? c / 100 : 0) < 6).ToList();
        var permaCharges = finalEffectiveCharges.Where(l => (int.TryParse(l.LawIdentifier, out int c) ? c / 100 : 0) == 6).ToList();

        float cappedBase = 0;
        if (normalCharges.Count > 0)
        {
            int highestCategory = normalCharges.Max(l => int.TryParse(l.LawIdentifier, out int c) ? c / 100 : 0);
            int highestCategoryMax = CategoryRanges.TryGetValue(highestCategory, out var range) ? range.Max : 0;
            float cap = highestCategoryMax * 1.5f;
            int baseSum = normalCharges.Sum(l => l.BaseSentence);
            cappedBase = Math.Min(baseSum, cap);

            @case.SentenceBreakdown.Add(new SentenceBreakdownEntry("sunrise-records-breakdown-base-sum", ("sum", baseSum)));
            if (baseSum > cap)
                @case.SentenceBreakdown.Add(new SentenceBreakdownEntry("sunrise-records-breakdown-cap", ("cat", highestCategory), ("cap", cap)));
        }

        int permaBaseSum = permaCharges.Sum(l => l.BaseSentence);
        if (permaCharges.Count > 0)
            @case.SentenceBreakdown.Add(new SentenceBreakdownEntry("sunrise-records-breakdown-perma-sum", ("sum", permaBaseSum)));

        float totalBase = cappedBase + permaBaseSum;

        // Modifiers (additive percentages applied to original BaseSentence)
        float globalModifierPercent = 0f;
        foreach (var circId in @case.Circumstances)
        {
            if (!circumstances.Contains(circId)) continue;
            if (_prototypeManager.TryIndex<CorporateLawPrototype>(circId, out var law))
            {
                float p = (law.SentenceMultiplier - 1.0f) * 100f;
                globalModifierPercent += p;
                @case.SentenceBreakdown.Add(new SentenceBreakdownEntry("sunrise-records-breakdown-modifier", ("id", Loc.GetString(law.Title)), ("percent", p.ToString("+0;-0"))));
            }
        }

        var pastLaws = allCases
            .Where(c => c.Id != @case.Id && (c.Status == CriminalCaseStatus.Finished || c.Status == CriminalCaseStatus.Incarcerated || c.Status == CriminalCaseStatus.Closed))
            .SelectMany(c => c.Laws)
            .ToHashSet();

        float totalModifierDelta = 0;
        foreach (var law in finalEffectiveCharges)
        {
            float lawRecidivismPercent = 0;
            if (pastLaws.Contains(law.ID))
            {
                lawRecidivismPercent = 15f;
                var lawName = law.LawIdentifier ?? Loc.GetString(law.Title);
                @case.SentenceBreakdown.Add(new SentenceBreakdownEntry("sunrise-records-breakdown-recidivism", ("id", lawName), ("percent", lawRecidivismPercent.ToString("+0;-0"))));
            }

            float totalLawPercent = globalModifierPercent + lawRecidivismPercent;
            totalModifierDelta += law.BaseSentence * (totalLawPercent / 100f);
        }

        float finalMinutes = totalBase + totalModifierDelta;
        
        // If there's any Category 6, ensure it's at least threshold
        if (permaCharges.Count > 0)
            finalMinutes = Math.Max(finalMinutes, threshold);

        if (finalMinutes < 1 && finalEffectiveCharges.Count > 0)
            finalMinutes = 1;

        int total = (int) Math.Round(finalMinutes);
        if (total > 0)
            isWarning = false;

        return total;
    }
}
