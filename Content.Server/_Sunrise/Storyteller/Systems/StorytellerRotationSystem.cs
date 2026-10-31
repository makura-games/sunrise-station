using Content.Server.GameTicking;
using Content.Shared._Sunrise.SunriseCCVars;
using Robust.Shared.Configuration;

namespace Content.Server._Sunrise.Storyteller.Systems;

/// <summary>
/// Handles persistent cooldown state for storyteller presets.
/// Puts the Insane storyteller on cooldown if it was just played,
/// and removes the cooldown if a different preset was played.
/// </summary>
public sealed partial class StorytellerRotationSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private GameTicker _ticker = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (!_cfg.GetCVar(SunriseCCVars.StorytellerRotationEnabled))
            return;

        if (ev.New != GameRunLevel.PreRoundLobby)
            return;

        if (ev.Old == GameRunLevel.InRound || ev.Old == GameRunLevel.PostRound)
        {
            var currentPreset = _ticker.CurrentPreset;
            if (currentPreset == null)
                return;

            var prevState = _cfg.GetCVar(SunriseCCVars.StorytellerRotationCounter);

            int nextState;
            // If Calm was played previously (state == 2) and now Insane is selected, we want to
            // reset back to 0 (return all modes). Otherwise, selecting Insane puts it on cooldown.
            if (currentPreset.ID == StorytellerSystem.StorytellerInsaneId)
            {
                nextState = prevState == 2 ? 0 : 1;
            }
            else if (currentPreset.ID == StorytellerSystem.StorytellerCalmId)
            {
                nextState = 2;
            }
            else
            {
                nextState = 0;
            }

            _cfg.SetCVar(SunriseCCVars.StorytellerRotationCounter, nextState);

            var sawmill = Logger.GetSawmill("storyteller");
            sawmill.Info($"Storyteller cooldown state updated to {nextState} after playing {currentPreset.ID}");
        }
    }
}
