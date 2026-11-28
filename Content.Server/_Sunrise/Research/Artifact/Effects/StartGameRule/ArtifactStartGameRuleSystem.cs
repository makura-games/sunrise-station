using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Shared.Xenoarchaeology.Artifact;
using Content.Shared.Xenoarchaeology.Artifact.XAE;

namespace Content.Server._Sunrise.Research.Artifact.Effects.StartGamerule;

public sealed partial class ArtifactStartGameRuleSystem : BaseXAESystem<ArtifactStartGameRuleComponent>
{
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private StationSystem _station = default!;

    protected override void OnActivated(Entity<ArtifactStartGameRuleComponent> ent, ref XenoArtifactNodeActivatedEvent args)
    {
        // Используемые этим эффектом правила выбирают точку появления на станции.
        if (_station.GetStations().Count == 0)
            return;

        foreach (var (rule, amount) in ent.Comp.Rules)
        {
            for (var i = 0; i < amount; i++)
            {
                _gameTicker.StartGameRule(rule);
            }
        }
    }
}
