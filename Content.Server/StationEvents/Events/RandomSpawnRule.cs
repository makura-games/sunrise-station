using Content.Server.GameTicking.Rules.Components;
using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking.Components;

namespace Content.Server.StationEvents.Events;

// Sunrise-Edit - логика радио-анонса вынесена в partial-файл
public sealed partial class RandomSpawnRule : StationEventSystem<RandomSpawnRuleComponent>
{
    protected override void Started(EntityUid uid, RandomSpawnRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        if (TryFindRandomTile(out _, out _, out _, out var coords))
        {
            Sawmill.Info($"Spawning {comp.Prototype} at {coords}");
            // Sunrise edit start - отправляем настраиваемый радио-анонс после случайного спавна
            var spawned = Spawn(comp.Prototype, coords);
            SendRadioAnnouncement(spawned, comp);
            // Sunrise edit end
        }
    }
}
