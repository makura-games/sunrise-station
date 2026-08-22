using Content.Server.AlertLevel;
using Content.Server._Sunrise.GameTicking.Rules.Components;
using Content.Server.GameTicking.Rules.Components;
using Robust.Shared.Timing;

namespace Content.Server.GameTicking.Rules;

public sealed partial class NukeopsRuleSystem
{
    [Dependency] private AlertLevelSystem _alertLevelSystem = default!;
    [Dependency] private IGameTiming _gameTiming = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<PendingNukeopsAlertLevelChangeComponent, NukeopsRuleComponent>();
        while (query.MoveNext(out var uid, out _, out var nukeops))
        {
            if (_gameTiming.CurTime < nukeops.AlertLevelChangeTime)
                continue;

            if (nukeops.SetAlertlevel == null || nukeops.TargetStation == null)
                continue;

            _alertLevelSystem.SetLevel(nukeops.TargetStation.Value, nukeops.SetAlertlevel, true, true, true, true);
            nukeops.AlertLevelChangeTime = default;
            RemCompDeferred<PendingNukeopsAlertLevelChangeComponent>(uid);
        }
    }

    private void ApplySunriseWarDeclarationAdjustments(Entity<NukeopsRuleComponent> ent)
    {
        ent.Comp.AlertLevelChangeTime = _gameTiming.CurTime + ent.Comp.AlertLevelDelay;
        EnsureComp<PendingNukeopsAlertLevelChangeComponent>(ent);
    }
}
