using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Content.Server._Sunrise.Storyteller.Components;
using Content.Shared._Sunrise.Storyteller.Prototypes;
using Content.Shared._Sunrise.SunriseCCVars;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Server._Sunrise.Storyteller.Systems;

public sealed partial class StorytellerSystem
{
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(8) // moderate timeout so we don't hold resources
    };

    private readonly System.Collections.Concurrent.ConcurrentQueue<Action> _pendingMainThreadActions = new();

    private void InitializeAI()
    {
        // Any specific AI setup can go here
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Safely drain background task callbacks on the main loop thread
        while (_pendingMainThreadActions.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log.Error($"Error executing storyteller main-thread callback: {ex}");
            }
        }
    }

    private void RequestAiEventRecommendation(Entity<StorytellerRuleComponent> entity, StationMetrics metrics)
    {
        var url = _cfg.GetCVar(SunriseCCVars.StorytellerAiUrl);
        if (string.IsNullOrWhiteSpace(url))
        {
            ExecuteHeuristicStoryteller(entity, metrics);
            return;
        }

        var payload = new AiRequestPayload
        {
            RoundDurationSeconds = GameTicker.RoundDuration().TotalSeconds,
            TotalPlayers = metrics.TotalPlayers,
            AlivePlayers = metrics.AliveCount,
            DeadPlayers = metrics.DeadCount,
            GhostPlayers = metrics.GhostCount,
            SecurityCount = metrics.SecurityCount,
            CargoBalance = metrics.CargoBalance,
            SciencePoints = metrics.SciencePoints,
            CrewStress = entity.Comp.CrewStress,
            ThreatBudget = entity.Comp.ThreatBudget,
            MajorThreatBudget = entity.Comp.MajorThreatBudget,
            PacingState = entity.Comp.PacingState.ToString(),
            RecentEvents = entity.Comp.EventHistory.Skip(Math.Max(0, entity.Comp.EventHistory.Count - 10)).ToList(),

            AtmosphereUnsafeRatio = metrics.AtmosphereUnsafeRatio,
            PowerGridDeficitRatio = metrics.PowerGridDeficitRatio,
            CrewWeaponCount = metrics.CrewWeaponCount,
            ActiveAntagonistCount = metrics.ActiveAntagonistCount,
            ActiveErtCount = metrics.ActiveErtCount,
            SingularityActive = metrics.SingularityActive,
            SingularityContained = metrics.SingularityContained,
            TeslaActive = metrics.TeslaActive,
            TeslaContained = metrics.TeslaContained,
            ResearchStorytellerScore = metrics.ResearchStorytellerScore,
            UnlockedTechnologyCount = metrics.UnlockedTechnologyCount,
            TotalTechnologyCount = metrics.TotalTechnologyCount,
            MaxResearchStorytellerScore = metrics.MaxResearchStorytellerScore,
            StationStrength = metrics.StationStrength,
            CrewRosterCount = metrics.CrewRosterCount,
            MaterialStrengthScore = metrics.MaterialStrengthScore,
            CrewDistribution = metrics.CrewDistribution,
            PlayerJoinRate = metrics.PlayerJoinRate,
            PlayerLeaveRate = metrics.PlayerLeaveRate,
            AvailableGhostRoles = metrics.AvailableGhostRoles
        };

        // Fire off request entirely on ThreadPool to ensure game thread is never blocked
        Task.Run(async () =>
        {
            try
            {
                var jsonPayload = JsonSerializer.Serialize(payload);
                using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                using var response = await _httpClient.PostAsync(url, content);
                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    var decision = JsonSerializer.Deserialize<AiDecision>(responseBody);

                    if (decision != null)
                    {
                        _pendingMainThreadActions.Enqueue(() => ApplyAiDecision(entity, decision, metrics));
                        return;
                    }
                }

                // If API returned a non-success status, fallback to heuristic
                Log.Warning($"Storyteller AI API returned non-success code: {response.StatusCode}. Falling back to heuristic.");
                _pendingMainThreadActions.Enqueue(() => ExecuteHeuristicStoryteller(entity, metrics));
            }
            catch (Exception ex)
            {
                Log.Warning($"Storyteller AI API call failed: {ex.Message}. Falling back to heuristic.");
                _pendingMainThreadActions.Enqueue(() => ExecuteHeuristicStoryteller(entity, metrics));
            }
        });
    }

    private void ApplyAiDecision(Entity<StorytellerRuleComponent> entity, AiDecision decision, StationMetrics metrics)
    {
        if (TerminatingOrDeleted(entity.Owner) || !Exists(entity.Owner))
            return;

        // Apply stress adjustments if specified by AI
        if (decision.AdjustStress.HasValue)
        {
            entity.Comp.CrewStress = Math.Clamp(entity.Comp.CrewStress + decision.AdjustStress.Value, 0f, 100f);
        }

        if (decision.AdjustBudget.HasValue)
        {
            entity.Comp.ThreatBudget = Math.Clamp(entity.Comp.ThreatBudget + decision.AdjustBudget.Value, 0f, entity.Comp.MaxThreatBudget);
            entity.Comp.MajorThreatBudget = Math.Clamp(entity.Comp.MajorThreatBudget + decision.AdjustBudget.Value, 0f, entity.Comp.MaxThreatBudget);
        }

        if (!string.IsNullOrEmpty(decision.Message))
        {
            Log.Info($"AI Storyteller Message: {decision.Message}");
        }

        // Spawn recommended event if specified
        if (!string.IsNullOrWhiteSpace(decision.SpawnEvent))
        {
            if (ProtoMan.TryIndex<EntityPrototype>(decision.SpawnEvent, out var proto))
            {
                if (ProtoMan.TryIndex<StorytellerMetadataPrototype>(proto.ID, out var metadata))
                {
                    TriggerEvent(entity, proto, metadata);
                    return;
                }
            }
            Log.Warning($"AI Storyteller recommended event '{decision.SpawnEvent}' which is not a valid game rule with storyteller metadata.");
        }

        // If AI chose not to spawn an event, we still ran successfully
    }

    private sealed class AiRequestPayload
    {
        public double RoundDurationSeconds { get; set; }
        public int TotalPlayers { get; set; }
        public int AlivePlayers { get; set; }
        public int DeadPlayers { get; set; }
        public int GhostPlayers { get; set; }
        public int SecurityCount { get; set; }
        public int CargoBalance { get; set; }
        public int SciencePoints { get; set; }
        public float CrewStress { get; set; }
        public float ThreatBudget { get; set; }
        public float MajorThreatBudget { get; set; }
        public string PacingState { get; set; } = string.Empty;
        public List<string> RecentEvents { get; set; } = new();

        public float AtmosphereUnsafeRatio { get; set; }
        public float PowerGridDeficitRatio { get; set; }
        public int CrewWeaponCount { get; set; }
        public int ActiveAntagonistCount { get; set; }
        public int ActiveErtCount { get; set; }
        public bool SingularityActive { get; set; }
        public bool SingularityContained { get; set; }
        public bool TeslaActive { get; set; }
        public bool TeslaContained { get; set; }
        public float ResearchStorytellerScore { get; set; }
        public int UnlockedTechnologyCount { get; set; }
        public int TotalTechnologyCount { get; set; }
        public float MaxResearchStorytellerScore { get; set; }
        public float StationStrength { get; set; }
        public int CrewRosterCount { get; set; }
        public float MaterialStrengthScore { get; set; }
        public Dictionary<string, int> CrewDistribution { get; set; } = new();
        public float PlayerJoinRate { get; set; }
        public float PlayerLeaveRate { get; set; }
        public int AvailableGhostRoles { get; set; }
    }

    private sealed class AiDecision
    {
        public string? SpawnEvent { get; set; }
        public float? AdjustBudget { get; set; }
        public float? AdjustStress { get; set; }
        public string? Message { get; set; }
    }
}
