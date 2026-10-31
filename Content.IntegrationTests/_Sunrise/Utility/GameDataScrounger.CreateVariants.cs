#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Robust.Shared.Utility;
using YamlDotNet.RepresentationModel;

namespace Content.IntegrationTests.Utility;

public static partial class GameDataScrounger
{
    private const string CreateVariantsTag = "!type:CreateVariants";
    private const string PartialOnlyTag = "!PartialOnly";
    private const string RemoveTag = "!Remove";

    private static readonly List<EntityPrototypePatch> EntityPrototypePatches = [];

    private sealed record EntityPrototypePatch(
        string Id,
        List<string>? Parents,
        HashSet<string> Components,
        HashSet<string> RemovedComponents);

    private static void IndexPartialPrototype(YamlMappingNode entry, YamlScalarNode type, string file)
    {
        if (type.Value != "entity" || !entry.TryGetNode("id", out YamlNode? idNode))
            return;

        var ids = GetPrototypeIds(idNode, file);
        entry.TryGetNode("components", out YamlSequenceNode? components);
        var (addedComponents, removedComponents) = GetComponentOperations(components);
        var replacesParents = entry.TryGetNode("parent", out _);

        for (var i = 0; i < ids.Count; i++)
        {
            EntityPrototypePatches.Add(new EntityPrototypePatch(
                ids[i],
                replacesParents ? GetVariantParents(entry, i, ids.Count, file) : null,
                new HashSet<string>(addedComponents),
                new HashSet<string>(removedComponents)));
        }
    }

    private static void ApplyEntityPrototypePatches()
    {
        foreach (var patch in EntityPrototypePatches)
        {
            // !PartialOnly не создаёт прототип, если полного определения больше нет.
            if (!_entitiesMetaIndex!.TryGetValue(patch.Id, out var entity))
                continue;

            if (patch.Parents is not null)
                entity.Parents = patch.Parents;

            foreach (var component in patch.RemovedComponents)
            {
                entity.Components.Remove(component);
                entity.RemovedComponents.Add(component);
            }

            foreach (var component in patch.Components)
            {
                entity.RemovedComponents.Remove(component);
                entity.Components.Add(component);
            }
        }
    }

    private static (HashSet<string> Added, HashSet<string> Removed) GetComponentOperations(
        YamlSequenceNode? components)
    {
        var added = new HashSet<string>();
        var removed = new HashSet<string>();

        if (components is null)
            return (added, removed);

        foreach (var node in components.Children)
        {
            if (node is not YamlMappingNode component ||
                !component.TryGetNode("type", out YamlScalarNode? componentType) ||
                componentType.Value is not { } type)
            {
                continue;
            }

            if (HasTag(component, RemoveTag))
            {
                added.Remove(type);
                removed.Add(type);
            }
            else
            {
                removed.Remove(type);
                added.Add(type);
            }
        }

        return (added, removed);
    }

    private static List<string> GetPrototypeIds(YamlNode idNode, string file)
    {
        if (idNode is YamlScalarNode scalar)
            return [scalar.AsString()];

        if (idNode is not YamlMappingNode mapping || !HasTag(mapping, CreateVariantsTag) ||
            !mapping.TryGetNode("values", out YamlSequenceNode? values))
        {
            throw new InvalidDataException($"Unsupported prototype id in {file}: {idNode}");
        }

        return values.Children.Select(node => node.AsString()).ToList();
    }

    private static List<string> GetVariantParents(
        YamlMappingNode entry,
        int variantIndex,
        int variantCount,
        string file)
    {
        if (!entry.TryGetNode("parent", out var parentNode))
            return [];

        if (parentNode is YamlScalarNode scalar)
            return [scalar.AsString()];

        if (parentNode is YamlSequenceNode sequence)
            return sequence.Children.Select(node => node.AsString()).ToList();

        if (parentNode is not YamlMappingNode mapping || !HasTag(mapping, CreateVariantsTag))
            throw new InvalidDataException($"Unsupported prototype parent in {file}: {parentNode}");

        if (mapping.TryGetNode("values", out YamlSequenceNode? values))
        {
            ValidateVariantCount(values, variantCount, file, "parent values");
            return [values.Children[variantIndex].AsString()];
        }

        if (mapping.TryGetNode("sequences", out YamlSequenceNode? sequences))
        {
            ValidateVariantCount(sequences, variantCount, file, "parent sequences");

            if (sequences.Children[variantIndex] is not YamlSequenceNode parents)
                throw new InvalidDataException($"Expected a parent sequence in {file}: {sequences.Children[variantIndex]}");

            return parents.Children.Select(node => node.AsString()).ToList();
        }

        throw new InvalidDataException($"CreateVariants parent in {file} has neither values nor sequences.");
    }

    private static void ValidateVariantCount(YamlSequenceNode variants, int expected, string file, string field)
    {
        if (variants.Children.Count != expected)
        {
            throw new InvalidDataException(
                $"CreateVariants {field} in {file} has {variants.Children.Count} entries, expected {expected}.");
        }
    }

    private static bool HasTag(YamlNode node, string tag)
    {
        return !node.Tag.IsEmpty && node.Tag.Value.Equals(tag, StringComparison.OrdinalIgnoreCase);
    }
}
