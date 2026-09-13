using System.Linq;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Disposal.Unit
{
    public sealed partial class AutoLoaderSystem : EntitySystem
    {
        [Dependency] private DisposableSystem _disposableSystem = default!;
        [Dependency] private SharedContainerSystem _containerSystem = default!;
        [Dependency] private SharedTransformSystem _xformSystem = default!;
        [Dependency] private EntityWhitelistSystem _whitelistSystem = default!;

        public void Cycle(EntityUid entity, Entity<AutoLoaderComponent> autoloader, BaseContainer autoloaderContainer, EntityUid currentTube)
        {
            var holder = Spawn(autoloader.Comp.HolderPrototypeId, _xformSystem.GetMapCoordinates(autoloader, xform: Transform(autoloader)));
            var holderComponent = Comp<DisposalHolderComponent>(holder);

            foreach (var item in autoloaderContainer.ContainedEntities.ToArray())
            {
                if (entity != item)
                    _containerSystem.Insert(item, holderComponent.Container);
            }

            if (_whitelistSystem.IsWhitelistPass(autoloader.Comp.Whitelist, entity))
                _containerSystem.Insert(entity, autoloaderContainer);
            else
                _containerSystem.Insert(entity, holderComponent.Container);

            _disposableSystem.EnterTube(holder, currentTube, holderComponent);
        }
    }

    [RegisterComponent]
    public sealed partial class AutoLoaderComponent : Component
    {
        [DataField(required: true)]
        public string Container = default!;

        [DataField]
        public EntityWhitelist? Whitelist;

        [DataField]
        public EntProtoId HolderPrototypeId = "DisposalHolder";
    }
}
