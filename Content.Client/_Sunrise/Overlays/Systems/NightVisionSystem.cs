using Content.Shared._Sunrise.NightVision.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;
using Content.Shared._Sunrise.NightVision;

namespace Content.Client._Sunrise.Overlays;

public sealed partial class NightVisionSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private TransformSystem _xformSys = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    private const string NightVisionShaderPrototype = "ModernNightVisionShader";
    private NightVisionOverlay _overlay = default!;
    [ViewVariables]
    private EntityUid? _effect = null;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NightVisionComponent, ComponentShutdown>(OnVisionShutdown);
        SubscribeLocalEvent<NightVisionComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<NightVisionComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<NightVisionComponent, AfterAutoHandleStateEvent>(OnHandleVisionState);
        _overlay = new(_prototypeManager.Index<ShaderPrototype>(NightVisionShaderPrototype));
    }

    private void OnHandleVisionState(Entity<NightVisionComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        AttemptAddVision(ent.Owner, ent.Comp);
    }

    private void OnPlayerAttached(Entity<NightVisionComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        AttemptAddVision(ent.Owner, ent.Comp);
    }

    private void OnPlayerDetached(Entity<NightVisionComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        AttemptRemoveVision(ent.Owner, true);
    }

    private void OnVisionShutdown(Entity<NightVisionComponent> ent, ref ComponentShutdown args)
    {
        AttemptRemoveVision(ent.Owner);
    }

    private void AttemptAddVision(EntityUid uid, NightVisionComponent comp)
    {
        if (_player.LocalSession?.AttachedEntity != uid)
            return;

        //only add if effect isnt already used
        if (_effect != null)
            return;

        _overlayMan.AddOverlay(_overlay);

        _effect = SpawnAttachedTo(comp.Effect, Transform(uid).Coordinates);
        _xformSys.SetParent(_effect.Value, uid);
    }

    /// <summary>
    /// Attempt to remove the overlay from the local player.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="force">Use if you need to forcefully remove the overlay no matter what. Only should be used with events that ONLY the local player can fire, like attach/detach</param>
    private void AttemptRemoveVision(EntityUid uid, bool force = false)
    {
        //ENSURE this is the local player
        if (_player.LocalSession?.AttachedEntity != uid && !force)
            return;

        _overlayMan.RemoveOverlay(_overlay);
        Del(_effect);
        _effect = null;
    }
}
