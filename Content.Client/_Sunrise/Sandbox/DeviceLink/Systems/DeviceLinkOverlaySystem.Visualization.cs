using Content.Client._Sunrise.Sandbox.DeviceLink.Overlays;
using Content.Shared._Sunrise.Sandbox;
using Robust.Client.Graphics;
using Robust.Shared.Random;

namespace Content.Client._Sunrise.Sandbox.DeviceLink.Systems;

public sealed partial class DeviceLinkOverlaySystem
{
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private IRobustRandom _random = default!;

    public Dictionary<EntityUid, List<EntityUid>> Rays { get; } = new();
    public Dictionary<EntityUid, Color> SourceColors { get; } = new();

    private static readonly Color[] RayColors =
    [
        Color.Red,
        Color.Orange,
        Color.Yellow,
        Color.YellowGreen,
        Color.LimeGreen,
        Color.LightBlue,
        Color.Blue,
        Color.HotPink,
        Color.BlueViolet,
    ];

    private DeviceLinkDebugOverlay? _overlay;

    private void InitializeVisualization()
    {
        SubscribeNetworkEvent<DeviceLinkOverlayDataEvent>(OnDebugOverlayData);
    }

    private void ShutdownVisualization()
    {
        RemoveOverlay();
    }

    private void ToggleOverlay()
    {
        if (_overlay == null)
        {
            _overlay = new();
            _overlayMan.AddOverlay(_overlay);
        }
        else
        {
            RemoveOverlay();
        }
    }

    private void RemoveOverlay()
    {
        SourceColors.Clear();

        _overlayMan.RemoveOverlay<DeviceLinkDebugOverlay>();
        _overlay?.Dispose();
        _overlay = null;
    }

    private void OnDebugOverlayData(DeviceLinkOverlayDataEvent args)
    {
        if (_overlay == null)
            return;

        Rays.Clear();

        foreach (var ray in args.Rays)
        {
            List<EntityUid> entities = [];

            var source = GetEntity(ray.Source);

            if (!source.Valid || Transform(source).MapUid is null)
                continue;

            foreach (var connection in ray.Connections)
            {
                var entity = GetEntity(connection);

                if (!entity.Valid || Transform(entity).MapUid is null)
                    continue;

                entities.Add(entity);
            }

            if (entities.Count == 0)
                continue;

            if (!SourceColors.ContainsKey(source))
                SourceColors.Add(source, _random.Pick(RayColors));

            Rays[source] = entities;
        }
    }
}
