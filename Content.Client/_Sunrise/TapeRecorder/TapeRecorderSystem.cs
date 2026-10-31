using Content.Shared._Sunrise.TapeRecorder;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Client._Sunrise.TapeRecorder;

/// <summary>
/// Client-side system for tape recorder functionality.
/// </summary>
public sealed partial class TapeRecorderSystem : SharedTapeRecorderSystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TapeRecorderComponent, AfterAutoHandleStateEvent>(OnRecorderState);
        SubscribeLocalEvent<TapeCassetteComponent, AfterAutoHandleStateEvent>(OnCassetteState);
    }

    private void OnRecorderState(Entity<TapeRecorderComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateRecorderUi(ent);
    }

    private void OnCassetteState(Entity<TapeCassetteComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        var parent = Transform(ent).ParentUid;

        if (!TryComp<TapeRecorderComponent>(parent, out var recorder))
            return;

        if (recorder.Cassette != ent.Owner)
            return;

        UpdateRecorderUi((parent, recorder));
    }

    private void UpdateRecorderUi(Entity<TapeRecorderComponent> ent)
    {
        if (_ui.TryGetOpenUi(ent.Owner, TapeRecorderUiKey.Key, out var bui))
            bui.Update();
    }
}
