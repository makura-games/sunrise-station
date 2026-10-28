using System.Numerics;
using Content.Shared._Sunrise.Smell.Prototypes;

namespace Content.Shared._Sunrise.Smell;

/// <summary>
/// Personal scent generator: turns a character seed (name, age, gender, voice)
/// into stable color and notes from the profile pools. The FNV-1a hash is
/// deterministic across runs.
/// </summary>
public static class ScentSignatureGenerator
{
    /// <summary>
    /// Builds the signature: the color comes from the seeded hash (HSV with fixed
    /// saturation and value ranges), one note is picked from each profile pool.
    /// </summary>
    public static ScentSignature Generate(
        string seed,
        PersonalScentProfilePrototype profile)
    {
        ulong colorHash = GetStableHash(seed, 0);

        float hue = (colorHash & 0xFFFF) / 65535f;
        float saturation = 0.45f + ((colorHash >> 16) & 0xFF) / 255f * 0.20f;
        float value = 0.75f + ((colorHash >> 24) & 0xFF) / 255f * 0.15f;

        Color color = Color.FromHsv(new Vector4(hue, saturation, value, 1f));
        List<LocId> notes = new(profile.NotePools.Count);

        for (int index = 0; index < profile.NotePools.Count; index++)
        {
            ScentNotePool pool = profile.NotePools[index];

            if (pool.Notes.Count == 0)
                continue;

            ulong noteHash = GetStableHash(seed, (ulong) index + 1);
            int noteIndex = (int) (noteHash % (ulong) pool.Notes.Count);

            notes.Add(pool.Notes[noteIndex]);
        }

        return new ScentSignature(color, notes);
    }

    /// <summary>
    /// Stable FNV-1a hash of a string with a salt: different salts yield independent
    /// samples for the color and each note.
    /// </summary>
    private static ulong GetStableHash(string value, ulong salt)
    {
        ulong hash = 14695981039346656037UL ^ salt;

        foreach (char character in value)
        {
            hash ^= character;
            hash *= 1099511628211UL;
        }

        return hash;
    }
}
