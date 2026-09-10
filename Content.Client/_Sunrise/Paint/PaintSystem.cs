using Content.Shared._Sunrise.Paint;

namespace Content.Client._Sunrise.Paint;

public sealed class PaintSystem : SharedPaintSystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PaintComponent, AfterAutoHandleStateEvent>(OnPaintState);
    }

    private void OnPaintState(Entity<PaintComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_ui.TryGetOpenUi<PaintBoundUserInterface>(ent.Owner, PaintUiKey.Key, out var bui))
            bui.Update();
    }
}
