using System.Text;
using Content.Server._Sunrise.Speech.Components;
using Content.Shared.Speech;
using Content.Shared.Speech.EntitySystems;

namespace Content.Server._Sunrise.Speech.EntitySystems;

public sealed partial class SS13AccentSystem : EntitySystem
{
    [Dependency] private ReplacementAccentSystem _replacement = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<SS13AccentComponent, AccentGetEvent>(OnAccent);
    }

    public string Accentuate(string message)
    {
        var accentedMessage = new StringBuilder(_replacement.ApplyReplacements(message, "ss13"));

        return accentedMessage.ToString();
    }

    private void OnAccent(Entity<SS13AccentComponent> ent, ref AccentGetEvent args)
    {
        args.Message = Accentuate(args.Message);
    }
}
