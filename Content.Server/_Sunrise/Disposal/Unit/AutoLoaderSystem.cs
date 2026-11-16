using System.Linq;
using Content.Server.Disposal.Holder;
using Content.Shared.Disposal.Unit;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Disposal.Unit
{
    public sealed partial class AutoLoaderSystem : EntitySystem
    {
        [Dependency] private DisposalHolderSystem _disposalHolder = default!;
        [Dependency] private SharedContainerSystem _containerSystem = default!;
        [Dependency] private SharedTransformSystem _xformSystem = default!;
        [Dependency] private EntityWhitelistSystem _whitelistSystem = default!;

        public void Cycle(EntityUid entity, Entity<AutoLoaderComponent> autoloader, BaseContainer autoloaderContainer, EntityUid currentTube)
        {
            var holder = Spawn(autoloader.Comp.HolderPrototypeId, _xformSystem.GetMapCoordinates(autoloader, xform: Transform(autoloader)));
            var holderComponent = Comp<DisposalHolderComponent>(holder);
            var holderContainer = holderComponent.Container ??
                                  _containerSystem.EnsureContainer<Container>(holder, nameof(DisposalHolderComponent));

            foreach (var item in autoloaderContainer.ContainedEntities.ToArray())
            {
                if (entity != item)
                    _containerSystem.Insert(item, holderContainer);
            }

            if (_whitelistSystem.IsWhitelistPass(autoloader.Comp.Whitelist, entity))
                _containerSystem.Insert(entity, autoloaderContainer);
            else
                _containerSystem.Insert(entity, holderContainer);

            _disposalHolder.TryEnterTube((holder, holderComponent), (currentTube, null));
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
