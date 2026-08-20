using Content.Server.Preferences.Managers;
using Content.Shared.Database;
using Content.Shared.Preferences;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.Administration.Systems;

public sealed partial class AdminVerbSystem
{
    [Dependency] private readonly IServerPreferencesManager _preferences = default!;

    private void AddSpawnHereVerbs(GetVerbsEvent<Verb> args, ActorComponent targetActor)
    {
        var preferences = _preferences.GetPreferences(targetActor.PlayerSession.UserId);

        foreach (var (slot, profile) in preferences.Characters)
        {
            if (profile is not HumanoidCharacterProfile humanoid)
                continue;

            args.Verbs.Add(new Verb
            {
                Text = $"{slot}. {humanoid.Name}",
                Category = VerbCategory.Spawn,
                Act = () => TrySpawnHere(args.Target, args.User, humanoid),
                ConfirmationPopup = true,
                Impact = LogImpact.High,
            });
        }
    }

    private bool TrySpawnHere(EntityUid target, EntityUid admin, HumanoidCharacterProfile profile)
    {
        if (!CanSpawnHere(target, admin, out var coordinates))
            return false;

        SpawnHere(target, coordinates, profile);
        return true;
    }

    private bool CanSpawnHere(EntityUid target, EntityUid admin, out EntityCoordinates coordinates)
    {
        if (_transformSystem.TryGetMapOrGridCoordinates(target, out var nullableCoordinates))
        {
            coordinates = nullableCoordinates.Value;
            return true;
        }

        coordinates = default;
        _popup.PopupEntity(Loc.GetString("admin-player-spawn-failed"), admin, admin);
        return false;
    }

    private void SpawnHere(EntityUid target, EntityCoordinates coordinates, HumanoidCharacterProfile profile)
    {
        var stationUid = _stations.GetOwningStation(target);
        var mobUid = _spawning.SpawnPlayerMob(coordinates, null, profile, stationUid);

        if (_mindSystem.TryGetMind(target, out var mindId, out var mind))
            _mindSystem.TransferTo(mindId, mobUid, true, mind: mind);
    }
}
