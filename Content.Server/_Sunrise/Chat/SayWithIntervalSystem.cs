using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Chat;

public sealed partial class SayWithIntervalSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private ChatSystem _chat = default!;

    /// <summary>
    /// The maximum amount of time between checking if advertisements should be displayed
    /// </summary>
    private readonly TimeSpan _maximumNextCheckDuration = TimeSpan.FromMilliseconds(20);

    /// <summary>
    /// The next time the game will check if advertisements should be displayed
    /// </summary>
    private TimeSpan _nextCheckTime = TimeSpan.MinValue;

    public override void Initialize()
    {
        SubscribeLocalEvent<SayWithIntervalComponent, MapInitEvent>(OnMapInit);


        _nextCheckTime = TimeSpan.MinValue;
    }

    private void OnMapInit(EntityUid uid, SayWithIntervalComponent advert, MapInitEvent args)
    {
        RandomizeNextAdvertTime(advert);
        _nextCheckTime = MathHelper.Min(advert.NextMessageTime, _nextCheckTime);
    }

    private void RandomizeNextAdvertTime(SayWithIntervalComponent advert)
    {
        var minDuration = Math.Max(1, advert.MinimumWait);
        var maxDuration = Math.Max(minDuration, advert.MaximumWait);
        var waitDuration = TimeSpan.FromSeconds(_random.Next(minDuration, maxDuration));

        advert.NextMessageTime = _gameTiming.CurTime + waitDuration;
    }

    public void SayAdvertisement(EntityUid uid, SayWithIntervalComponent? advert = null)
    {
        if (!Resolve(uid, ref advert))
            return;

        InGameICChatType type = InGameICChatType.Speak;
        switch(advert.chatType)
        {
            case "whisper": type = InGameICChatType.Whisper; break;
            case "emote": type = InGameICChatType.Emote; break;
            case "collectiveMind": type = InGameICChatType.CollectiveMind; break;
            default:  type = InGameICChatType.Speak; break;
        } // Костыль, но InGameICChatType не указывается ингейм
        _chat.TrySendInGameICMessage(uid, advert.Message, type, false, isFormatted: advert.Format);
    }

    public override void Update(float frameTime)
    {
        var currentGameTime = _gameTiming.CurTime;
        if (_nextCheckTime > currentGameTime)
            return;

        // _nextCheckTime starts at TimeSpan.MinValue, so this has to SET the value, not just increment it.
        _nextCheckTime = currentGameTime + _maximumNextCheckDuration;

        var query = EntityQueryEnumerator<SayWithIntervalComponent>();
        while (query.MoveNext(out var uid, out var advert))
        {
            if (currentGameTime > advert.NextMessageTime)
            {
                SayAdvertisement(uid, advert);
                // The timer is always refreshed when it expires, to prevent mass advertising (ex: all the vending machines have no power, and get it back at the same time).
                RandomizeNextAdvertTime(advert);
            }
            _nextCheckTime = MathHelper.Min(advert.NextMessageTime, _nextCheckTime);
        }
    }


}
