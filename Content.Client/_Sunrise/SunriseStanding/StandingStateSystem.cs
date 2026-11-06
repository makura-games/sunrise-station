using Content.Shared.Standing;
using Robust.Client.GameObjects;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._Sunrise.SunriseStanding;

public sealed partial class StandingStateSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StandingStateComponent, AppearanceChangeEvent>(OnAppearanceChanged);
    }

    private void OnAppearanceChanged(EntityUid uid, StandingStateComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var drawDepth = !component.Standing ? (int)DrawDepth.SmallMobs : (int)DrawDepth.Mobs;

        _sprite.SetDrawDepth((uid, args.Sprite), drawDepth);
    }
}
