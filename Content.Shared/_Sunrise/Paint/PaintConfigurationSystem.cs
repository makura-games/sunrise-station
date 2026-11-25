namespace Content.Shared._Sunrise.Paint;

public sealed partial class PaintConfigurationSystem : EntitySystem
{
    [SubscribeLocalEvent]
    private void OnSetColor(Entity<PaintComponent> ent, ref PaintSetColorMessage args)
    {
        if (!args.UiKey.Equals(PaintUiKey.Key))
            return;

        TrySetColor(ent.AsNullable(), args.Color);
    }

    public bool TrySetColor(Entity<PaintComponent?> ent, Color color)
    {
        if (!Resolve(ent, ref ent.Comp) || !CanSetColor(color))
            return false;

        color = color.WithAlpha(1f);
        if (ent.Comp.Color == color)
            return true;

        ent.Comp.Color = color;
        Dirty(ent);
        return true;
    }

    public static bool CanSetColor(Color color)
    {
        return color.R is >= 0f and <= 1f
            && color.G is >= 0f and <= 1f
            && color.B is >= 0f and <= 1f;
    }
}
