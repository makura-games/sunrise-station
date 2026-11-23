using Robust.Shared.GameObjects;

namespace Content.Shared._Sunrise.Humanoid;

public static class SunrisePrototypeLayerData
{
    /// <summary>
    /// Creates a detached copy of mutable sprite layer data.
    /// </summary>
    public static PrototypeLayerData Clone(PrototypeLayerData source)
    {
        return new PrototypeLayerData
        {
            Shader = source.Shader,
            TexturePath = source.TexturePath,
            RsiPath = source.RsiPath,
            State = source.State,
            Scale = source.Scale,
            Rotation = source.Rotation,
            Offset = source.Offset,
            Visible = source.Visible,
            Color = source.Color,
            MapKeys = source.MapKeys is not null ? new HashSet<string>(source.MapKeys) : null,
            RenderingStrategy = source.RenderingStrategy,
            CopyToShaderParameters = Clone(source.CopyToShaderParameters),
            Cycle = source.Cycle,
            Loop = source.Loop,
        };
    }

    /// <summary>
    /// Applies values explicitly present in <paramref name="overrideData"/> over a detached copy of <paramref name="baseData"/>.
    /// </summary>
    public static PrototypeLayerData Merge(PrototypeLayerData baseData, PrototypeLayerData overrideData)
    {
        var data = Clone(baseData);
        Apply(data, overrideData);
        return data;
    }

    /// <summary>
    /// Applies non-null override data to an existing layer data object.
    /// </summary>
    public static void Apply(PrototypeLayerData target, PrototypeLayerData overrideData)
    {
        if (overrideData.Shader is not null)
            target.Shader = overrideData.Shader;

        if (overrideData.TexturePath is not null)
            target.TexturePath = overrideData.TexturePath;

        if (overrideData.RsiPath is not null)
            target.RsiPath = overrideData.RsiPath;

        if (overrideData.State is not null)
            target.State = overrideData.State;

        if (overrideData.Scale is not null)
            target.Scale = overrideData.Scale;

        if (overrideData.Rotation is not null)
            target.Rotation = overrideData.Rotation;

        if (overrideData.Offset is not null)
            target.Offset = overrideData.Offset;

        if (overrideData.Visible is not null)
            target.Visible = overrideData.Visible;

        if (overrideData.Color is not null)
            target.Color = overrideData.Color;

        if (overrideData.MapKeys is not null)
            target.MapKeys = new HashSet<string>(overrideData.MapKeys);

        if (overrideData.RenderingStrategy is not null)
            target.RenderingStrategy = overrideData.RenderingStrategy;

        if (overrideData.CopyToShaderParameters is not null)
            target.CopyToShaderParameters = Clone(overrideData.CopyToShaderParameters);

        if (overrideData.Cycle)
            target.Cycle = true;

        if (!overrideData.Loop)
            target.Loop = false;
    }

    private static PrototypeCopyToShaderParameters? Clone(PrototypeCopyToShaderParameters? source)
    {
        if (source is null)
            return null;

        return new PrototypeCopyToShaderParameters
        {
            LayerKey = source.LayerKey,
            ParameterTexture = source.ParameterTexture,
            ParameterUV = source.ParameterUV,
        };
    }
}
