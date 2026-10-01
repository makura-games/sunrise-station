using Content.Server.Lathe;
using Content.Server.Lathe.Components;
using Content.Shared._Sunrise.Lathe;
using Content.Shared.Lathe;
using Content.Shared.Power;

namespace Content.Server._Sunrise.Lathe;

public sealed class SunriseLatheProgressSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LatheComponent, LatheStartPrintingEvent>(OnLatheStartPrinting);
        SubscribeLocalEvent<SunriseLatheProgressComponent, PowerChangedEvent>(
            OnPowerChanged,
            after: [typeof(LatheSystem)]);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SunriseLatheProgressComponent, LatheComponent>();
        while (query.MoveNext(out var uid, out var progress, out var lathe))
        {
            if (progress.State == SunriseLatheProgressState.Running &&
                lathe.CurrentRecipe == null)
            {
                RemCompDeferred<SunriseLatheProgressComponent>(uid);
            }
        }
    }

    private void OnLatheStartPrinting(Entity<LatheComponent> ent, ref LatheStartPrintingEvent args)
    {
        if (!TryComp<LatheProducingComponent>(ent, out var producing))
            return;

        var progress = EnsureComp<SunriseLatheProgressComponent>(ent);
        progress.StartTime = producing.StartTime;
        progress.EndTime = producing.StartTime + producing.ProductionLength;
        progress.State = SunriseLatheProgressState.Running;
        Dirty(ent, progress);
    }

    private void OnPowerChanged(Entity<SunriseLatheProgressComponent> ent, ref PowerChangedEvent args)
    {
        if (!args.Powered)
        {
            ent.Comp.State = SunriseLatheProgressState.Interrupted;
            Dirty(ent);
            return;
        }

        if (ent.Comp.State == SunriseLatheProgressState.Interrupted &&
            !HasComp<LatheProducingComponent>(ent))
        {
            RemCompDeferred<SunriseLatheProgressComponent>(ent);
        }
    }
}
