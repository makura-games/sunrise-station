using System.Text;
using Content.Server._Sunrise.Speech.Components;
using Content.Shared.Speech;
using Content.Shared.Speech.EntitySystems;

namespace Content.Server._Sunrise.Speech.EntitySystems;

public sealed partial class ItalianAccentSystem : EntitySystem
{
    [Dependency] private ReplacementAccentSystem _replacement = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<ItalianAccentComponent, AccentGetEvent>(OnAccent);
    }

    public string Accentuate(string message)
    {
        return _replacement.ApplyReplacements(message, "italian");
    }

    private void OnAccent(Entity<ItalianAccentComponent> ent, ref AccentGetEvent args)
    {
        args.Message = Accentuate(args.Message);
    }
}
