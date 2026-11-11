using Content.Shared._Sunrise.BloodCult;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client._Sunrise.BloodCult.Narsie;

public sealed partial class NarsieVisualizer : VisualizerSystem<NarsieComponent>
{
    private static readonly Animation NarsieSpawnAnimation = new()
    {
        Length = TimeSpan.FromSeconds(3.5),
        AnimationTracks =
        {
            new AnimationTrackSpriteFlick()
            {
                LayerKey = NarsieLayer.Default,
                KeyFrames = { new AnimationTrackSpriteFlick.KeyFrame(new RSI.StateId("narsie_spawn_anim"), 0f) }
            }
        }
    };

    [Dependency] private AnimationPlayerSystem _animationSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NarsieComponent, AnimationCompletedEvent>(OnAnimationCompleted);
    }

    private void OnAnimationCompleted(EntityUid uid, NarsieComponent component, AnimationCompletedEvent args)
    {
        SetDefaultState(Comp<SpriteComponent>(uid));
    }

    protected override void OnAppearanceChange(EntityUid uid, NarsieComponent component, ref AppearanceChangeEvent args)
    {
        base.OnAppearanceChange(uid, component, ref args);

        if (args.Sprite == null)
            return;

        if (!args.AppearanceData.TryGetValue(NarsieVisualState.VisualState, out var narsieVisualsObject) ||
            narsieVisualsObject is not NarsieVisuals narsieVisual)
            return;

        switch (narsieVisual)
        {
            case NarsieVisuals.Spawning:
                PlaySpawnAnimation(uid);
                break;
            case NarsieVisuals.Spawned:
                if (_animationSystem.HasRunningAnimation(uid, "narsie_spawn"))
                    break;
                SetDefaultState(args.Sprite);
                break;
        }
    }

    private void PlaySpawnAnimation(EntityUid uid)
    {
        _animationSystem.Play(uid, NarsieSpawnAnimation, "narsie_spawn");
    }

    private void SetDefaultState(SpriteComponent component)
    {
        component.LayerSetVisible(NarsieLayer.Default, true);
        component.LayerSetState(NarsieLayer.Default, new RSI.StateId("narsie"));
        component.LayerSetAutoAnimated(NarsieLayer.Default, true);
    }
}
