using Content.Client.Interactable.Components;
using Content.Client.Graphics;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client.Stealth;

public sealed partial class StealthSystem : SharedStealthSystem
{
    private static readonly ProtoId<ShaderPrototype> Shader = "Stealth";
    private static readonly ProtoId<ShaderPrototype> NoMirageShader = "NoMirageStealth";

    [Dependency] private SharedTransformSystem _transformSystem = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private ShaderInstance _shader = default!;
    private ShaderInstance _noMirageShader = default!; // Sunrise-Edit

    public override void Initialize()
    {
        base.Initialize();

        _shader = ProtoMan.Index(Shader).InstanceUnique();
        _noMirageShader = ProtoMan.Index(NoMirageShader).InstanceUnique(); // Sunrise-Edit

        SubscribeLocalEvent<StealthComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<StealthComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<StealthComponent, BeforePostShaderRenderEvent>(OnShaderRender);
    }

    public override void SetEnabled(EntityUid uid, bool value, StealthComponent? component = null)
    {
        if (!Resolve(uid, ref component) || component.Enabled == value)
            return;

        base.SetEnabled(uid, value, component);
        SetShader(uid, value, component);
    }

    private void SetShader(EntityUid uid, bool enabled, StealthComponent? component = null, SpriteComponent? sprite = null)
    {
        if (!Resolve(uid, ref component, ref sprite, false))
            return;

        _sprite.SetColor((uid, sprite), Color.White);
        if (enabled)
        {
            var shader = component.Mirage ? _shader : _noMirageShader; // Sunrise-Edit - сохраняем вариант невидимости без миража
            _sprite.SetPostShader((uid, sprite), new SpriteComponent.PostShaderArgs(ContentPostShaderIds.Stealth, shader)
            {
                GetScreenTexture = true,
                RaiseShaderEvent = true,
                Before = ContentPostShaderIds.BeforeOutlines,
            });
        }
        else
        {
            _sprite.RemovePostShader((uid, sprite), ContentPostShaderIds.Stealth);
        }

        if (!enabled)
        {
            if (component.HadOutline && !TerminatingOrDeleted(uid))
                EnsureComp<InteractionOutlineComponent>(uid);
            return;
        }

        if (HasComp<InteractionOutlineComponent>(uid))
            component.HadOutline = true;
    }

    private void OnStartup(EntityUid uid, StealthComponent component, ComponentStartup args)
    {
        SetShader(uid, component.Enabled, component);
    }

    private void OnShutdown(EntityUid uid, StealthComponent component, ComponentShutdown args)
    {
        if (!Terminating(uid))
            SetShader(uid, false, component);
    }

    private void OnShaderRender(EntityUid uid, StealthComponent component, BeforePostShaderRenderEvent args)
    {
        // Distortion effect uses screen coordinates. If a player moves, the entities appear to move on screen. this
        // makes the distortion very noticeable.

        // So we need to use relative screen coordinates. The reference frame we use is the parent's position on screen.
        // this ensures that if the Stealth is not moving relative to the parent, its relative screen position remains
        // unchanged.
        var parent = Transform(uid).ParentUid;
        if (!parent.IsValid())
            return; // should never happen, but lets not kill the client.
        var parentXform = Transform(parent);
        var reference = args.Viewport.WorldToLocal(_transformSystem.GetWorldPosition(parentXform));
        reference.X = -reference.X;
        var visibility = GetVisibility(uid, component);

        // actual visual visibility effect is limited to +/- 1.
        visibility = Math.Clamp(visibility, -1f, 1f);
        // Sunrise start
        var shaderToUse = component.Mirage ? _shader : _noMirageShader;
        shaderToUse.SetParameter("reference", reference);
        shaderToUse.SetParameter("visibility", visibility);
        shaderToUse.SetParameter("shimmer_frequency", component.ShimmerFrequency);
        // Sunrise end

        visibility = MathF.Max(0, visibility);
        _sprite.SetColor((uid, args.Sprite), new Color(visibility, visibility, 1, 1));
    }
}
