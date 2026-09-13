using Content.Shared.Chat;
using Content.Server.Chat.Systems;
using Robust.Shared.Prototypes;
using Content.Shared._Sunrise.Animations;

namespace Content.Server.Speech;

public sealed partial class EmotesMenuSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeAllEvent<PlayEmoteMessage>(OnPlayEmote);
    }

    private void OnPlayEmote(PlayEmoteMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;
        if (!player.HasValue)
            return;

        if (!_prototypeManager.TryIndex(msg.ProtoId, out var proto) || proto.ChatTriggers.Count == 0)
        {
            if (!HasComp<EmoteAnimationComponent>(player))
                return;
        }

        _chat.TryEmoteWithChat(player.Value, msg.ProtoId);
    }
}
