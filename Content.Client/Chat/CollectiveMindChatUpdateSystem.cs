using Content.Client.Chat.Managers;
using Content.Shared._Sunrise.CollectiveMind;
using Robust.Client.Player;

namespace Content.Client.Chat
{
    public sealed partial class CollectiveMindChatUpdateSystem : EntitySystem
    {
        [Dependency] private IChatManager _chatManager = default!;
        [Dependency] private IPlayerManager _playerManager = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<CollectiveMindComponent, ComponentInit>(OnInit);
            SubscribeLocalEvent<CollectiveMindComponent, ComponentRemove>(OnRemove);
        }

        public CollectiveMindComponent? Player => CompOrNull<CollectiveMindComponent>(_playerManager.LocalSession?.AttachedEntity);
        public bool IsCollectiveMind => Player != null;

        private void OnInit(EntityUid uid, CollectiveMindComponent component, ComponentInit args)
        {
            _chatManager.UpdatePermissions();
        }

        private void OnRemove(EntityUid uid, CollectiveMindComponent component, ComponentRemove args)
        {
            _chatManager.UpdatePermissions();
        }
    }
}
