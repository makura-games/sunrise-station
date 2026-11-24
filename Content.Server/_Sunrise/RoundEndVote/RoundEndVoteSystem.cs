using Content.Server.GameTicking;
using Content.Server.RoundEnd;
using Content.Server.Voting.Managers;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.Voting;
using Robust.Shared.Timing;
using Robust.Shared.Configuration;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.RoundEndVote;

public sealed partial class RoundEndVoteSystem : EntitySystem
{
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private IVoteManager _voteManager = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private TimeSpan? _voteStartTime;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundEndSystemChangedEvent>(OnRoundEndSystemChange);
    }

    public void OnRoundEndSystemChange(RoundEndSystemChangedEvent args)
    {
        _voteStartTime = _gameTiming.CurTime + _gameTicker.LobbyDuration - TimeSpan.FromSeconds(_cfg.GetCVar(SunriseCCVars.VotingsDelay));
        Log.Warning($"Vote will start at {_voteStartTime}");

        if (_cfg.GetCVar(SunriseCCVars.ResetPresetAfterRestart))
            _gameTicker.SetGamePreset("Secret");
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_gameTicker.RunLevel != GameRunLevel.PreRoundLobby || _voteStartTime == null)
            return;

        if (_gameTiming.CurTime >= _voteStartTime)
        {
            StartRoundEndVotes();
            _voteStartTime = null;
        }
    }

    public void StartRoundEndVotes()
    {
        if (_gameTicker.RunLevel != GameRunLevel.PreRoundLobby)
            return;

        if (_cfg.GetCVar(SunriseCCVars.RunMapVoteAfterRestart))
            _voteManager.CreateStandardVote(null, StandardVoteType.Map);

        if (_cfg.GetCVar(SunriseCCVars.RunPresetVoteAfterRestart))
            _voteManager.CreateStandardVote(null, StandardVoteType.Preset);
    }
}
