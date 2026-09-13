using Content.Shared._Sunrise.Humanoid.Events;
using Content.Shared._Sunrise.Humanoid;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Robust.Server.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Humanoid;

public sealed partial class HumanoidPhysicalStatsSystem : EntitySystem
{
    [Dependency] private PhysicsSystem _physics = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SunriseHumanoidProfileComponent, SunriseHumanoidProfileChangedEvent>(OnProfileChanged);
    }

    private void OnProfileChanged(Entity<SunriseHumanoidProfileComponent> ent, ref SunriseHumanoidProfileChangedEvent args)
    {
        ApplyPhysicalStats(ent, args.Species, args.Width, args.Height);
    }

    private void ApplyPhysicalStats(Entity<SunriseHumanoidProfileComponent> ent, ProtoId<SpeciesPrototype> speciesId, float width, float height)
    {
        if (!_proto.TryIndex(speciesId, out var species))
            return;

        if (!TryComp<FixturesComponent>(ent.Owner, out var fixtures))
            return;

        var weightMultiplier = GetWeightMultiplier(species, width, height);
        var physicalStats = EnsureComp<HumanoidPhysicalStatsComponent>(ent.Owner);

        foreach (var (fixtureId, fixture) in fixtures.Fixtures)
        {
            if (!physicalStats.BaseDensities.TryGetValue(fixtureId, out var baseDensity))
            {
                baseDensity = fixture.Density;
                physicalStats.BaseDensities[fixtureId] = baseDensity;
            }

            _physics.SetDensity(ent.Owner, fixtureId, fixture, baseDensity * weightMultiplier, manager: fixtures);
        }
    }

    private static float GetWeightMultiplier(SpeciesPrototype species, float width, float height)
    {
        var defaultWeight = GetProfileWeight(species, species.DefaultWidth, species.DefaultHeight);
        if (MathF.Abs(defaultWeight) < 0.0001f)
            return 1f;

        return MathF.Max(0.01f, GetProfileWeight(species, width, height) / defaultWeight);
    }

    private static float GetProfileWeight(SpeciesPrototype species, float width, float height)
    {
        return species.StandardWeight + species.StandardDensity * (width * height - 1f);
    }
}
