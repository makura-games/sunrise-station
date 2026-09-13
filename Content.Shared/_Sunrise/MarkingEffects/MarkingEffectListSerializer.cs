using Robust.Shared.IoC;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;

namespace Content.Shared._Sunrise.MarkingEffects;

public sealed class MarkingEffectListSerializer :
    ITypeSerializer<List<MarkingEffect>, SequenceDataNode>,
    ITypeCopier<List<MarkingEffect>>
{
    public List<MarkingEffect> Read(
        ISerializationManager serializationManager,
        SequenceDataNode node,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<List<MarkingEffect>>? instanceProvider = null)
    {
        var effects = instanceProvider != null
            ? instanceProvider()
            : new List<MarkingEffect>(node.Count);

        effects.Clear();

        foreach (var dataNode in node)
        {
            effects.Add(ReadEffectOrFallback(serializationManager, dataNode, hookCtx, context));
        }

        return effects;
    }

    public ValidationNode Validate(
        ISerializationManager serializationManager,
        SequenceDataNode node,
        IDependencyCollection dependencies,
        ISerializationContext? context = null)
    {
        var list = new List<ValidationNode>(node.Count);

        foreach (var dataNode in node)
        {
            list.Add(dataNode switch
            {
                ValueDataNode valueNode => ValidateValue(valueNode),
                MappingDataNode mappingNode when CanReadLegacyMapping(mappingNode) => new ValidatedValueNode(mappingNode),
                _ => new ErrorNode(dataNode, $"Expected {nameof(ValueDataNode)} for marking effect.")
            });
        }

        return new ValidatedSequenceNode(list);
    }

    public DataNode Write(
        ISerializationManager serializationManager,
        List<MarkingEffect> value,
        IDependencyCollection dependencies,
        bool alwaysWrite = false,
        ISerializationContext? context = null)
    {
        var sequence = new SequenceDataNode(value.Count);

        foreach (var effect in value)
        {
            sequence.Add(new ValueDataNode(effect?.ToString() ?? ColorMarkingEffect.White.ToString()));
        }

        return sequence;
    }

    public void CopyTo(
        ISerializationManager serializationManager,
        List<MarkingEffect> source,
        ref List<MarkingEffect> target,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null)
    {
        target.Clear();
        target.EnsureCapacity(source.Count);

        foreach (var effect in source)
        {
            target.Add(effect?.Clone() ?? ColorMarkingEffect.White.Clone());
        }
    }

    private static MarkingEffect ReadEffectOrFallback(
        ISerializationManager serializationManager,
        DataNode node,
        SerializationHookContext hookCtx,
        ISerializationContext? context)
    {
        return TryReadEffect(serializationManager, node, hookCtx, context, out var effect)
            ? effect
            : ColorMarkingEffect.White.Clone();
    }

    private static bool TryReadEffect(
        ISerializationManager serializationManager,
        DataNode node,
        SerializationHookContext hookCtx,
        ISerializationContext? context,
        out MarkingEffect effect)
    {
        try
        {
            effect = node switch
            {
                ValueDataNode valueNode => ReadValueEffect(valueNode),
                MappingDataNode mappingNode => ReadLegacyMappingEffect(serializationManager, mappingNode, hookCtx, context),
                _ => null
            } ?? ColorMarkingEffect.White.Clone();

            return true;
        }
        catch
        {
            effect = ColorMarkingEffect.White.Clone();
            return false;
        }
    }

    private static MarkingEffect? ReadValueEffect(ValueDataNode node)
    {
        if (node.IsNull || ValueDataNode.IsNullLiteral(node.Value) || string.IsNullOrWhiteSpace(node.Value))
            return null;

        return MarkingEffect.Parse(node.Value);
    }

    private static MarkingEffect? ReadLegacyMappingEffect(
        ISerializationManager serializationManager,
        MappingDataNode node,
        SerializationHookContext hookCtx,
        ISerializationContext? context)
    {
        if (node.Tag?.StartsWith("!type:") ?? false)
            return serializationManager.Read<MarkingEffect>(node, hookCtx, context, notNullableOverride: true);

        if (IsRoughGradientMapping(node))
            return serializationManager.Read<RoughGradientMarkingEffect>(node, hookCtx, context, notNullableOverride: true);

        if (IsGradientMapping(node))
            return serializationManager.Read<GradientMarkingEffect>(node, hookCtx, context, notNullableOverride: true);

        return ReadColorMappingEffect(node);
    }

    private static ValidationNode ValidateValue(ValueDataNode node)
    {
        if (ReadValueEffect(node) != null)
            return new ValidatedValueNode(node);

        return new ErrorNode(node, $"Failed parsing {nameof(MarkingEffect)}.");
    }

    private static bool CanReadLegacyMapping(MappingDataNode node)
    {
        return (node.Tag?.StartsWith("!type:") ?? false)
            || node.Has("colors")
            || IsGradientMapping(node)
            || IsRoughGradientMapping(node);
    }

    private static bool IsGradientMapping(MappingDataNode node)
    {
        return node.Has("offset")
            || node.Has("size")
            || node.Has("rotation")
            || node.Has("speed")
            || node.Has("pixelated")
            || node.Has("mirrored");
    }

    private static bool IsRoughGradientMapping(MappingDataNode node)
    {
        return node.Has("horizontal");
    }

    private static ColorMarkingEffect ReadColorMappingEffect(MappingDataNode node)
    {
        if (!node.TryGet<MappingDataNode>("colors", out var colors))
            node.TryGet("Colors", out colors);

        if (colors != null
            && colors.TryGet<ValueDataNode>("base", out var baseColorNode)
            && Color.TryFromHex(baseColorNode.Value) is { } color)
        {
            return new ColorMarkingEffect(color);
        }

        return ColorMarkingEffect.White.Clone();
    }
}
