using System.Numerics;
using Content.Client.Body;
using Content.Client.Clothing;
using Content.Client.Sprite;
using Content.Shared.Sprite;
using Content.Shared._Sunrise.Humanoid;
using Content.Shared._Sunrise.Humanoid.Events;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Client._Sunrise.Humanoid;

public sealed partial class SunriseHumanoidProfileVisualSystem : EntitySystem
{
    [Dependency] private ClientClothingSystem _clothing = default!;
    [Dependency] private VisualBodySystem _visualBody = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private ScaleVisualsSystem _scaleVisuals = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SunriseHumanoidProfileComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SunriseHumanoidProfileComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<SunriseHumanoidProfileComponent, SunriseHumanoidProfileChangedEvent>(OnProfileChanged);
        SubscribeLocalEvent<HumanoidProfileComponent, AfterAutoHandleStateEvent>(OnHumanoidProfileState);
        SubscribeLocalEvent<SunriseHumanoidProfileComponent, AppearanceChangeEvent>(OnAppearanceChanged, after: [typeof(ScaleVisualsSystem)]);
    }

    private void OnStartup(Entity<SunriseHumanoidProfileComponent> ent, ref ComponentStartup args)
    {
        Refresh(ent.Owner);
    }

    private void OnState(Entity<SunriseHumanoidProfileComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        Refresh(ent.Owner);
    }

    private void OnProfileChanged(Entity<SunriseHumanoidProfileComponent> ent, ref SunriseHumanoidProfileChangedEvent args)
    {
        Refresh(ent.Owner);
    }

    private void OnHumanoidProfileState(Entity<HumanoidProfileComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!HasComp<SunriseHumanoidProfileComponent>(ent.Owner))
            return;

        Refresh(ent.Owner);
    }

    private void OnAppearanceChanged(Entity<SunriseHumanoidProfileComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite != null)
            _sprite.SetScale((ent, args.Sprite), _scaleVisuals.GetSpriteScale(ent) * new Vector2(ent.Comp.Width, ent.Comp.Height));
    }

    public void Refresh(Entity<SunriseHumanoidProfileComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false) ||
            !TryComp<SpriteComponent>(ent.Owner, out var sprite))
        {
            return;
        }

        _sprite.SetScale((ent.Owner, sprite), _scaleVisuals.GetSpriteScale(ent) * new Vector2(ent.Comp.Width, ent.Comp.Height));
        _visualBody.RefreshBodyTypeVisuals(ent.Owner);
        _clothing.RefreshEquipmentVisuals(ent.Owner);
    }
}
