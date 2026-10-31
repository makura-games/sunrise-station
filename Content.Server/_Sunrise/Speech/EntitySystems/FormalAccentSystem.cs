using Content.Server._Sunrise.Speech.Components;
using Content.Shared.Speech;
using Content.Shared.Speech.EntitySystems;

namespace Content.Server._Sunrise.Speech.EntitySystems;

/// <summary>
/// System that gives the speaker a formal accent by expanding abbreviations.
/// </summary>
public sealed partial class FormalAccentSystem : EntitySystem
{
    [Dependency] private ReplacementAccentSystem _replacement = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FormalAccentComponent, AccentGetEvent>(OnAccentGet);
    }

    public string Accentuate(string message, FormalAccentComponent component)
    {
        var msg = message;

        // Apply the formal accent word replacements
        msg = _replacement.ApplyReplacements(msg, "formal");

        return msg;
    }

    private void OnAccentGet(Entity<FormalAccentComponent> ent, ref AccentGetEvent args)
    {
        args.Message = Accentuate(args.Message, ent.Comp);
    }
}
