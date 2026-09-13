using System.Text;
using Content.Server._Sunrise.Speech.Components;
using Content.Server.Speech;
using Content.Server.Speech.Components;
using Content.Server.Speech.EntitySystems;
using Content.Shared.Speech;

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

    private void OnAccent(EntityUid uid, ItalianAccentComponent component, AccentGetEvent args)
    {
        args.Message = Accentuate(args.Message);
    }
}
