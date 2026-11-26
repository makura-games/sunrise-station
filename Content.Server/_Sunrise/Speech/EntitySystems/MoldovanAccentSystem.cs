using System.Text;
using Content.Server._Sunrise.Speech.Components;
using Content.Shared.Speech;
using Content.Shared.Speech.EntitySystems;

namespace Content.Server._Sunrise.Speech.EntitySystems;

public sealed partial class MoldovanAccentSystem : EntitySystem
{
    [Dependency] private ReplacementAccentSystem _replacement = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<MoldovanAccentComponent, AccentGetEvent>(OnAccent);
    }

    public string Accentuate(string message)
    {
        return _replacement.ApplyReplacements(message, "moldovan");
    }

    private void OnAccent(Entity<MoldovanAccentComponent> ent, ref AccentGetEvent args)
    {
        args.Message = Accentuate(args.Message);
    }
}
