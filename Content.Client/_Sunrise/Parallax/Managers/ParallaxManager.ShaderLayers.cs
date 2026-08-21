using System.Numerics;
using Content.Client._Sunrise;
using Content.Client.Parallax.Data;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

#pragma warning disable IDE0130 // Пространство имён совпадает с расширяемым vanilla-менеджером.
namespace Content.Client.Parallax.Managers;

public sealed partial class ParallaxManager : IPostInjectInit
{
    [Dependency] private readonly NetTexturesManager _netTexturesManager = default!;

    private static readonly ShaderParallaxTextureSource StarTextureSource = new();

    // Централизованная замена сохраняет upstream-прототипы карт без массового дублирования.
    private static readonly IReadOnlyDictionary<ResPath, ProtoId<ShaderPrototype>> StarShaderReplacements =
        new Dictionary<ResPath, ProtoId<ShaderPrototype>>
        {
            [new ResPath("/Prototypes/Parallaxes/parallax_config.toml")] = "SunriseParallaxStarsLowQuality",
            [new ResPath("/Prototypes/Parallaxes/parallax_config_stars.toml")] = "SunriseParallaxStarsBright",
            [new ResPath("/Prototypes/Parallaxes/parallax_config_stars_dim.toml")] = "SunriseParallaxStarsDim",
            [new ResPath("/Prototypes/Parallaxes/parallax_config_stars-2.toml")] = "SunriseParallaxStarsBrightFar",
            [new ResPath("/Prototypes/Parallaxes/parallax_config_stars_dim-2.toml")] = "SunriseParallaxStarsDimFar",
        };

    private static IParallaxTextureSource ResolveLayerTextureSource(
        ParallaxLayerConfig config,
        out string? shader)
    {
        shader = config.Shader;
        if (config.Texture is not GeneratedParallaxTextureSource generated ||
            !StarShaderReplacements.TryGetValue(generated.ParallaxConfigPath, out var replacement))
        {
            return config.Texture;
        }

        shader = replacement;
        return StarTextureSource;
    }

    private ShaderInstance? CreateLayerShader(
        IParallaxTextureSource textureSource,
        string? shader,
        out bool ownsShader)
    {
        ownsShader = false;
        if (textureSource is ShaderParallaxTextureSource &&
            (string.IsNullOrEmpty(shader) || string.Equals(shader, "unshaded", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"{nameof(ShaderParallaxTextureSource)} requires an explicit shader prototype");
        }

        if (string.IsNullOrEmpty(shader))
            return null;

        var prototype = _prototypeManager.Index<ShaderPrototype>(shader);
        if (textureSource is not ShaderParallaxTextureSource)
            return prototype.Instance();

        ownsShader = true;
        return prototype.InstanceUnique();
    }

    private static Vector2 GetLayerTextureSize(IParallaxTextureSource textureSource, Texture texture)
    {
        return textureSource is ShaderParallaxTextureSource source
            ? Vector2.Max(source.Size, Vector2.One)
            : texture.Size;
    }

    private void UnloadPreparedLayers(IEnumerable<ParallaxLayerPrepared> layers)
    {
        foreach (var layer in layers)
        {
            layer.TextureSource.Unload(_deps);
            if (layer.OwnsShader)
                layer.Shader?.Dispose();
        }
    }

    private void OnNetworkTexturesInvalidated()
    {
        var names = new HashSet<string>(_parallaxesLQ.Keys);
        names.UnionWith(_parallaxesHQ.Keys);
        names.UnionWith(_loadingParallaxes.Keys);

        foreach (var name in names)
        {
            UnloadParallax(name);
        }
    }

    void IPostInjectInit.PostInject()
    {
        _netTexturesManager.ResourcesInvalidated += OnNetworkTexturesInvalidated;
    }
}
