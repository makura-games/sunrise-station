using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared._Sunrise.TapePlayer;
using Robust.Client.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Network;

namespace Content.Client._Sunrise.TapePlayer
{
    public sealed partial class TapePlayerSystem : SharedTapePlayerSystem
    {
        [Dependency] private SharedAppearanceSystem _appearanceSystem = default!;
        [Dependency] private SharedUserInterfaceSystem _uiSystem = default!;
        [Dependency] private IConfigurationManager _cfg = default!;
        [Dependency] private INetManager _netManager = default!;

        private bool _tapePlayerClientEnabled;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<TapePlayerComponent, AppearanceChangeEvent>(OnAppearanceChange);
            SubscribeLocalEvent<TapePlayerComponent, AnimationCompletedEvent>(OnAnimationCompleted);
            SubscribeLocalEvent<TapePlayerComponent, AfterAutoHandleStateEvent>(OnTapePlayerAfterState);
            _cfg.OnValueChanged(SunriseCCVars.TapePlayerClientEnabled, OnTapePlayerClientOptionChanged, true);

            _netManager.Connected += OnConnected;
        }

        public override void Shutdown()
        {
            base.Shutdown();
            _cfg.UnsubValueChanged(SunriseCCVars.TapePlayerClientEnabled, OnTapePlayerClientOptionChanged);
        }

        private void OnTapePlayerClientOptionChanged(bool option)
        {
            _tapePlayerClientEnabled = option;
            if (_netManager.IsConnected)
                RaiseNetworkEvent(new ClientOptionTapePlayerEvent(_tapePlayerClientEnabled));
        }

        private async void OnConnected(object? sender, NetChannelArgs e)
        {
            RaiseNetworkEvent(new ClientOptionTapePlayerEvent(_tapePlayerClientEnabled));
        }

        private void OnTapePlayerAfterState(Entity<TapePlayerComponent> ent, ref AfterAutoHandleStateEvent args)
        {
            if (IsClientSide(ent))
                return;

            if (!_uiSystem.TryGetOpenUi<TapePlayerBoundUserInterface>(ent.Owner, TapePlayerUiKey.Key, out var bui))
                return;

            bui.Reload();
        }

        private void OnAnimationCompleted(EntityUid uid, TapePlayerComponent component, AnimationCompletedEvent args)
        {
            if (!TryComp<SpriteComponent>(uid, out var sprite))
                return;

            if (!TryComp<AppearanceComponent>(uid, out var appearance) ||
                !_appearanceSystem.TryGetData<TapePlayerVisualState>(uid, TapePlayerVisuals.VisualState, out var visualState, appearance))
            {
                visualState = TapePlayerVisualState.On;
            }

            UpdateAppearance(uid, visualState, component, sprite);
        }

        private void OnAppearanceChange(EntityUid uid, TapePlayerComponent component, ref AppearanceChangeEvent args)
        {
            if (args.Sprite == null)
                return;

            if (!args.AppearanceData.TryGetValue(TapePlayerVisuals.VisualState, out var visualStateObject) ||
                visualStateObject is not TapePlayerVisualState visualState)
            {
                visualState = TapePlayerVisualState.On;
            }

            UpdateAppearance(uid, visualState, component, args.Sprite);
        }

        private void UpdateAppearance(EntityUid uid, TapePlayerVisualState visualState, TapePlayerComponent component, SpriteComponent sprite)
        {
            SetLayerState(TapePlayerVisualLayers.Base, component.OffState, sprite);

            switch (visualState)
            {
                case TapePlayerVisualState.On:
                    SetLayerState(TapePlayerVisualLayers.Base, component.OnState, sprite);
                    break;

                case TapePlayerVisualState.Off:
                    SetLayerState(TapePlayerVisualLayers.Base, component.OffState, sprite);
                    break;
            }
        }

        private void SetLayerState(TapePlayerVisualLayers layer, string? state, SpriteComponent sprite)
        {
            if (string.IsNullOrEmpty(state))
                return;

            sprite.LayerSetVisible(layer, true);
            sprite.LayerSetAutoAnimated(layer, true);
            sprite.LayerSetState(layer, state);
        }
    }
}
