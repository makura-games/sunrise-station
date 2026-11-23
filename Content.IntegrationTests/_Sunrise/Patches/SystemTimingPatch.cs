#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._Sunrise.Patches;

/// <summary>
/// Читает уже собираемые Prometheus-метрики систем и печатает разницу за один тест.
/// </summary>
internal static class SystemTimingPatch
{
    private static readonly MethodInfo? _exportMethod = ResolveExportMethod();

    private static Dictionary<string, double> _snapshot = [];

    internal static void EnableMetrics(IEntitySystemManager systemManager)
    {
        systemManager.MetricsEnabled = true;
    }

    internal static async Task TakeSnapshot()
    {
        _snapshot = await CollectSums();
    }

    internal static async Task PrintTop10(TextWriter output)
    {
        var current = await CollectSums();
        if (current.Count == 0)
            return;

        var deltas = current
            .Select(pair => (Name: pair.Key, Delta: pair.Value - _snapshot.GetValueOrDefault(pair.Key)))
            .Where(entry => entry.Delta > 1e-6)
            .OrderByDescending(entry => entry.Delta)
            .Take(10)
            .ToList();

        if (deltas.Count == 0)
            return;

        await output.WriteLineAsync("  ┌─ Top 10 server systems by tick time");
        for (var i = 0; i < deltas.Count; i++)
        {
            await output.WriteLineAsync(
                $"  │ {i + 1,2}. {deltas[i].Name,-55} {deltas[i].Delta * 1000,8:F2} ms");
        }

        await output.WriteLineAsync("  └" + new string('─', 70));
    }

    private static async Task<Dictionary<string, double>> CollectSums()
    {
        if (_exportMethod == null)
            return [];

        var registry = ResolveDefaultRegistry();
        if (registry == null)
            return [];

        using var memory = new MemoryStream();
        var task = (Task?) _exportMethod.Invoke(registry, [memory, CancellationToken.None]);
        if (task != null)
            await task;

        return ParseSums(Encoding.UTF8.GetString(memory.ToArray()));
    }

    private static Dictionary<string, double> ParseSums(string text)
    {
        var result = new Dictionary<string, double>();
        const string prefix = "robust_entity_systems_update_usage_sum{system=\"";

        foreach (var line in text.AsSpan().EnumerateLines())
        {
            if (!line.StartsWith(prefix))
                continue;

            var afterPrefix = line[prefix.Length..];
            var nameEnd = afterPrefix.IndexOf('"');
            if (nameEnd < 0)
                continue;

            var name = afterPrefix[..nameEnd].ToString();
            var valuePart = afterPrefix[(nameEnd + 3)..];
            var spaceIndex = valuePart.IndexOf(' ');
            var valueSpan = spaceIndex >= 0 ? valuePart[..spaceIndex] : valuePart;

            if (double.TryParse(valueSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                result[name] = value;
        }

        return result;
    }

    private static MethodInfo? ResolveExportMethod()
    {
        var registry = ResolveDefaultRegistry();
        return registry?.GetType().GetMethod(
            "CollectAndExportAsTextAsync",
            [typeof(Stream), typeof(CancellationToken)]);
    }

    private static object? ResolveDefaultRegistry()
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(candidate => candidate.GetName().Name == "Prometheus.NetStandard");

        return assembly?.GetType("Prometheus.Metrics")
            ?.GetProperty("DefaultRegistry", BindingFlags.Public | BindingFlags.Static)
            ?.GetValue(null);
    }
}
