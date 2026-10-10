using Content.Shared.Chat;
using Content.Shared._Sunrise.Animations;

namespace Content.Shared.Speech.EntitySystems;

public sealed partial class EmotesMenuSystem : EntitySystem
{
    [Dependency] private SharedChatSystem _chat = default!;

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

        // Sunrise edit start - разрешаем эмоции без chat trigger для анимаций
        if (!ProtoMan.TryIndex(msg.ProtoId, out var proto) || proto.ChatTriggers.Count == 0)
        {
            if (!HasComp<EmoteAnimationComponent>(player))
                return;
        }
        // Sunrise edit end

        _chat.TryEmoteWithChat(player.Value, msg.ProtoId);
    }
}
