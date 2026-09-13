using System.Linq;
using Content.Shared._Sunrise;
using Content.Shared._Sunrise.Loadouts;
using Content.Shared._Sunrise.Preferences;
using Content.Shared._Sunrise.TTS;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Roles;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Enums;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared.Preferences;

public sealed partial class HumanoidCharacterProfile
{
    public ProtoId<TTSVoicePrototype> Voice => SunriseProfile.Voice;

    public ProtoId<BodyTypePrototype> BodyType => SunriseProfile.BodyType;

    public float Width => SunriseProfile.Width;

    public float Height => SunriseProfile.Height;

    public IReadOnlyDictionary<ProtoId<JobPrototype>, LocId> JobAlternativeTitles => SunriseProfile.JobAlternativeTitles;

    public HumanoidCharacterProfile WithVoice(string voice)
    {
        return new(this) { SunriseProfile = SunriseProfile.WithVoice(voice) };
    }

    public HumanoidCharacterProfile WithBodyType(string bodyType)
    {
        return new(this) { SunriseProfile = SunriseProfile.WithBodyType(bodyType) };
    }

    public HumanoidCharacterProfile WithWidth(float width)
    {
        return new(this) { SunriseProfile = SunriseProfile.WithWidth(width) };
    }

    public HumanoidCharacterProfile WithHeight(float height)
    {
        return new(this) { SunriseProfile = SunriseProfile.WithHeight(height) };
    }

    public HumanoidCharacterProfile WithSize(float width, float height)
    {
        return new(this) { SunriseProfile = SunriseProfile.WithSize(width, height) };
    }

    public HumanoidCharacterProfile WithJobAlternativeTitle(ProtoId<JobPrototype> jobId, LocId? alternativeTitle)
    {
        return new(this) { SunriseProfile = SunriseProfile.WithJobAlternativeTitle(jobId, alternativeTitle) };
    }

    public HumanoidCharacterProfile WithJobAlternativeTitles(Dictionary<ProtoId<JobPrototype>, LocId> alternativeTitles)
    {
        return new(this) { SunriseProfile = SunriseProfile.WithJobAlternativeTitles(alternativeTitles) };
    }

    public static bool CanHaveVoice(TTSVoicePrototype voice, Sex sex)
    {
        return voice.RoundStart && (sex == Sex.Unsexed || voice.Sex == sex || voice.Sex == Sex.Unsexed);
    }

    private void EnsureSunriseProfileValid(
        SpeciesPrototype species,
        Sex sex,
        ICommonSession session,
        IDependencyCollection collection,
        string[] sponsorPrototypes)
    {
        NormalizeSunriseLoadoutIds(collection.Resolve<IPrototypeManager>());
        SunriseProfile = SunriseProfile.Validated(this, species, sex, session, collection, sponsorPrototypes);
    }

    /// <summary>
    /// Возвращает лодауты, ошибочно сохранённые под ID из Sunrise-пула, к каноническим ID должностей.
    /// </summary>
    private void NormalizeSunriseLoadoutIds(IPrototypeManager prototypeManager)
    {
        foreach (var pool in prototypeManager.EnumeratePrototypes<LoadoutPoolPrototype>())
        {
            foreach (var (roleId, effectiveRoleId) in pool.RoleLoadouts)
            {
                if (roleId == effectiveRoleId || !_loadouts.Remove(effectiveRoleId.Id, out var loadout))
                    continue;

                if (_loadouts.ContainsKey(roleId.Id))
                    continue;

                loadout.Role = roleId;
                _loadouts.Add(roleId.Id, loadout);
            }
        }
    }

    private static HumanoidCharacterAppearance EnsureSunriseAppearanceValid(
        HumanoidCharacterAppearance appearance,
        string[] sponsorPrototypes,
        IPrototypeManager prototype)
    {
        var markings = appearance.Markings.ToDictionary(
            organ => organ.Key,
            organ => organ.Value.ToDictionary(
                layer => layer.Key,
                layer => layer.Value
                    .Where(marking =>
                        !prototype.TryIndex<MarkingPrototype>(marking.MarkingId, out var markingPrototype) ||
                        !markingPrototype.SponsorOnly ||
                        sponsorPrototypes.Contains(marking.MarkingId))
                    .Select(marking => marking.DeepClone())
                    .ToList()));

        return appearance.WithMarkings(markings);
    }
}
