using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.InteractionsPanel;

[Virtual]
public partial class InteractionsPanel : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private IAdminLogManager _log = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    public override void Initialize()
    {
        InitializeInteractions();
        InitializeUI();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateInteractions(frameTime);
    }
}
