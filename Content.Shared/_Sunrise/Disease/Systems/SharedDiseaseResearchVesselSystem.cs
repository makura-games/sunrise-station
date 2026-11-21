using Content.Shared._Sunrise.Disease.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;

namespace Content.Shared._Sunrise.Disease.Systems;

/// <summary>Собирает текст осмотра сосуда для исследования из реплицированных данных.</summary>
public abstract partial class SharedDiseaseResearchVesselSystem : EntitySystem
{
    [Dependency] private ItemSlotsSystem _itemSlots = default!;

    [SubscribeLocalEvent]
    private void OnExamined(Entity<DiseaseResearchVesselComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var comp = ent.Comp;
        if (!comp.HasSample || _itemSlots.GetItemOrNull(ent.Owner, comp.SampleSlotId) == null)
        {
            args.PushText(Loc.GetString("disease-vessel-empty"));
            return;
        }

        args.PushText(Loc.GetString("disease-vessel-sample", ("strain", comp.StrainCode),
            ("strength", comp.Strength), ("rate", comp.PointsPerSecond),
            ("resistance", comp.ResistancePercent)));
        args.PushText(Loc.GetString(comp.RemainingSeconds > 0 ? "disease-vessel-remaining" : "disease-vessel-exhausted",
            ("seconds", comp.RemainingSeconds)));
        if (comp.StudyPaused)
            args.PushText(Loc.GetString("disease-vessel-paused"));
    }
}
