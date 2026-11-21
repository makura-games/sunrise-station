using Content.Shared._Sunrise.Humanoid;
using Content.Shared.Body;
using Content.Shared.EntityEffects;
using Content.Shared.Humanoid;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Disease.EntityEffects;

/// <summary>
/// Обрабатывает <see cref="HairLoss"/>: снимает все маркировки указанных слоёв.
/// </summary>
public sealed partial class HairLossEntityEffectSystem : EntityEffectSystem<VisualBodyComponent, HairLoss>
{
    [Dependency] private SunriseHumanoidMarkingSystem _marking = default!;

    protected override void Effect(Entity<VisualBodyComponent> entity, ref EntityEffectEvent<HairLoss> args)
    {
        foreach (var layer in args.Effect.Layers)
        {
            _marking.ClearLayer(entity, layer);
        }
    }
}

/// <summary>
/// Выпадение волос: цель лишается причёски, а при желании и бороды.
/// </summary>
/// <remarks>
/// Отросшие заново у цирюльника волосы при следующем срабатывании выпадут снова.
/// </remarks>
public sealed partial class HairLoss : EntityEffectBase<HairLoss>
{
    /// <summary>
    /// Слои, которые очищаются целиком.
    /// </summary>
    [DataField]
    public List<HumanoidVisualLayers> Layers = new() { HumanoidVisualLayers.Hair };

    /// <inheritdoc/>
    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return Loc.GetString("entity-effect-guidebook-hair-loss", ("chance", Probability));
    }
}
