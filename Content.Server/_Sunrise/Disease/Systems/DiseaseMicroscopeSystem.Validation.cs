using Content.Shared._Sunrise.Disease.Components;

namespace Content.Server._Sunrise.Disease.Systems;

public sealed partial class DiseaseMicroscopeSystem
{
    // Общие проверки заготовки, версии состояния и идущей партии.
    private bool CanRun(
        Entity<DiseaseMicroscopeComponent> ent,
        int revision,
        out Entity<DiseaseStrainSampleComponent> sample,
        out string feedback)
    {
        sample = default;
        feedback = "stale";
        if (revision != ent.Comp.Revision)
            return false;

        feedback = "unpowered";
        if (TerminatingOrDeleted(ent) || IsPaused(ent) || !_power.IsPowered(ent.Owner))
            return false;

        feedback = "invalid-sample";
        return TryGetSample(ent, out sample) && sample.Owner == ent.Comp.SourceSample && !IsPaused(sample) &&
               ent.Comp.Stage == sample.Comp.Stage && ent.Comp.MaxStage == sample.Comp.MaxStage;
    }

    /// <summary>
    /// В слоте та же заготовка, на которой идёт партия, и её стадия не сдвинулась.
    /// </summary>
    protected override bool ValidateSample(Entity<DiseaseMicroscopeComponent> ent)
    {
        return !TerminatingOrDeleted(ent) && !IsPaused(ent) &&
               TryGetSample(ent, out var sample) && sample.Owner == ent.Comp.SourceSample && !IsPaused(sample) &&
               ent.Comp.Stage == sample.Comp.Stage && ent.Comp.MaxStage == sample.Comp.MaxStage;
    }
}
