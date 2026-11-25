using Content.Shared._Sunrise.Paint;

namespace Content.Client._Sunrise.Paint;

public sealed partial class ClientPaintSystem : EntitySystem
{
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    [SubscribeLocalEvent]
    private void OnPaintState(Entity<PaintComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_ui.TryGetOpenUi(ent.Owner, PaintUiKey.Key, out var bui))
            bui.Update();
    }
}
