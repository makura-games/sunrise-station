using Content.Shared.Verbs;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Sunrise.Paint;

public sealed class PaintConfigurationSystem : EntitySystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    private const string SettingsIconPath = "/Textures/Interface/VerbIcons/settings.svg.192dpi.png";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PaintComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        Subs.BuiEvents<PaintComponent>(PaintUiKey.Key, subs => subs.Event<PaintSetColorMessage>(OnSetColor));
    }

    private void OnGetVerbs(Entity<PaintComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !_ui.HasUi(ent.Owner, PaintUiKey.Key))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("paint-configure-verb"),
            Icon = new SpriteSpecifier.Texture(new ResPath(SettingsIconPath)),
            Act = () => _ui.OpenUi(ent.Owner, PaintUiKey.Key, user),
        });
    }

    private void OnSetColor(Entity<PaintComponent> ent, ref PaintSetColorMessage args)
    {
        TrySetColor(ent.AsNullable(), args.Color);
    }

    private bool TrySetColor(Entity<PaintComponent?> ent, Color color)
    {
        if (!Resolve(ent, ref ent.Comp) || !CanSetColor(color))
            return false;

        DoSetColor((ent.Owner, ent.Comp), color);
        return true;
    }

    private static bool CanSetColor(Color color) =>
        IsValid(color.R) && IsValid(color.G) && IsValid(color.B);

    private static bool IsValid(float value) =>
        value is >= 0f and <= 1f;

    private void DoSetColor(Entity<PaintComponent> ent, Color color)
    {
        var newColor = new Color(color.R, color.G, color.B);
        if (ent.Comp.Color == newColor)
            return;

        ent.Comp.Color = newColor;
        Dirty(ent);
    }
}

[Serializable, NetSerializable]
public enum PaintUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class PaintSetColorMessage(Color color) : BoundUserInterfaceMessage
{
    public readonly Color Color = color;
}
