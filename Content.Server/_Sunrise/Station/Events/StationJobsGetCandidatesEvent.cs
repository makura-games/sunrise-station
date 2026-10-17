using Content.Shared.Preferences;

namespace Content.Server.Station.Events;

public readonly partial record struct StationJobsGetCandidatesEvent
{
    public HumanoidCharacterProfile? Profile { get; init; }
}
