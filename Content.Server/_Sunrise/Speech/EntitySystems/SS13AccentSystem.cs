using System.Text;
using Content.Server._Sunrise.Speech.Components;
using Content.Server.Speech;
using Content.Server.Speech.Components;
using Content.Server.Speech.EntitySystems;
using Content.Shared.Speech;

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

    private void OnAccent(EntityUid uid, SS13AccentComponent component, AccentGetEvent args)
    {
        args.Message = Accentuate(args.Message);
    }
}
