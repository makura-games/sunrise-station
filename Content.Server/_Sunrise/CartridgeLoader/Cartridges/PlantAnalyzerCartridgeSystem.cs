using Content.Server.Botany.Components;
using Content.Shared.Botany.Components;
using Content.Shared.CartridgeLoader;
using Robust.Server.GameObjects;

namespace Content.Server.CartridgeLoader.Cartridges;

public sealed partial class PlantAnalyzerCartridgeSystem : EntitySystem
{
    [Dependency] private CartridgeLoaderSystem _cartridgeLoaderSystem = null!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlantAnalyzerCartridgeComponent, CartridgeAddedEvent>(OnCartridgeAdded);
        SubscribeLocalEvent<PlantAnalyzerCartridgeComponent, CartridgeRemovedEvent>(OnCartridgeRemoved);
    }

    private void OnCartridgeAdded(Entity<PlantAnalyzerCartridgeComponent> ent, ref CartridgeAddedEvent args)
    {
        var loader = args.Loader;

        // ТОЛЬКО добавляем компонент анализатора
        EnsureComp<PlantAnalyzerComponent>(loader);
    }

    private void OnCartridgeRemoved(Entity<PlantAnalyzerCartridgeComponent> ent, ref CartridgeRemovedEvent args)
    {
        var loader = args.Loader;

        // Удаляем компонент только если больше нет картриджей с этим функционалом
        if (!_cartridgeLoaderSystem.HasProgram<PlantAnalyzerCartridgeComponent>(loader))
        {
            RemComp<PlantAnalyzerComponent>(loader);
        }
    }
}
