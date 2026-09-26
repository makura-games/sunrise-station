using Content.Shared.Chat.Prototypes;
using Content.Shared.Speech.Components;
using Robust.Shared.Prototypes;

#pragma warning disable IDE0130
namespace Content.Shared.Speech.EntitySystems;

public sealed partial class VocalSystem
{
    public void SetEmoteSounds(
        Entity<VocalComponent?> ent,
        ProtoId<EmoteSoundsPrototype>? emoteSounds)
    {
        if (!Resolve(ent, ref ent.Comp, false) || ent.Comp.EmoteSounds == emoteSounds)
            return;

        ent.Comp.EmoteSounds = emoteSounds;
        Dirty(ent);
    }
}
