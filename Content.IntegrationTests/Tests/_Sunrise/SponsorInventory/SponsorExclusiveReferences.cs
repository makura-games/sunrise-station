using System.Collections.Generic;
using YamlDotNet.RepresentationModel;

namespace Content.IntegrationTests.Tests._Sunrise.SponsorInventory;

/// <summary>
/// Находит ссылки на эксклюзивные предметы во всех ветках YAML, включая ключи словарей и вложенные таблицы.
/// </summary>
internal static class SponsorExclusiveReferences
{
    public static List<string> Find(YamlNode root, IReadOnlySet<string> exclusiveIds, bool prototypeFile)
    {
        var references = new List<string>();
        var visited = new HashSet<YamlNode>(ReferenceEqualityComparer.Instance);

        if (prototypeFile && root is YamlSequenceNode prototypes)
        {
            for (var i = 0; i < prototypes.Children.Count; i++)
            {
                var node = prototypes.Children[i];
                if (node is not YamlMappingNode mapping ||
                    !mapping.Children.TryGetValue(new YamlScalarNode("type"), out var type) ||
                    type is not YamlScalarNode)
                {
                    Visit(node, $"[{i}]");
                    continue;
                }

                foreach (var (key, value) in mapping.Children)
                {
                    // ID объявления и parent относятся к типу самого прототипа; это не выдача предмета.
                    if (key is YamlScalarNode { Value: "id" or "parent" })
                        continue;

                    Visit(value, $"[{i}].{key}");
                    Visit(key, $"[{i}].ключ");
                }
            }
        }
        else
        {
            Visit(root, "$root");
        }

        return references;

        void Visit(YamlNode node, string path)
        {
            if (!visited.Add(node))
                return;

            switch (node)
            {
                case YamlScalarNode scalar when scalar.Value != null && exclusiveIds.Contains(scalar.Value):
                    references.Add($"{path}: {scalar.Value}");
                    break;
                case YamlSequenceNode sequence:
                    for (var i = 0; i < sequence.Children.Count; i++)
                        Visit(sequence.Children[i], $"{path}[{i}]");
                    break;
                case YamlMappingNode mapping:
                    foreach (var (key, value) in mapping.Children)
                    {
                        Visit(key, $"{path}.ключ");
                        Visit(value, $"{path}.{key}");
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Учитывает старые ID, которые загрузчик карты заменяет эксклюзивными прототипами.
    /// </summary>
    public static void IncludeMigratedIds(HashSet<string> exclusiveIds, IReadOnlyDictionary<string, string> renames)
    {
        bool changed;
        do
        {
            changed = false;
            foreach (var (oldId, newId) in renames)
            {
                if (exclusiveIds.Contains(newId))
                    changed |= exclusiveIds.Add(oldId);
            }
        } while (changed);
    }
}
