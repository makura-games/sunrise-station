using Content.Shared._Sunrise.Biocode;
using Content.Shared.Item;

#pragma warning disable IDE0130 // Namespace не соответствует расположению файла
namespace Content.Shared.Throwing;

public sealed partial class ThrowingSystem
{
    private bool TryApplySunriseThrowModifiers(EntityUid thrown, EntityUid? user, ref float throwSpeed)
    {
        if (user != null && HasComp<BiocodeComponent>(thrown))
        {
            var ev = new AttemptThrowBiocodeEvent(thrown, user);
            RaiseLocalEvent(thrown, ref ev);
            if (ev.Cancelled)
                return false;
        }

        if (!TryComp<ItemComponent>(thrown, out var item) || !ProtoMan.TryIndex(item.Size, out var size))
            return true;

        if (size.Weight > 4)
            throwSpeed *= 1f - MathF.Min(0.6f, (size.Weight - 4) * 0.06f);

        return true;
    }
}
