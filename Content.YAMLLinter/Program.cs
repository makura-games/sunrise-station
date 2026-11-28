using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Content.IntegrationTests;
using Content.IntegrationTests._Sunrise.Patches;
using Content.IntegrationTests.Utility;
using Robust.Shared.Prototypes;
using Robust.Shared.Reflection;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.UnitTesting;
using Robust.UnitTesting.Pool;

namespace Content.YAMLLinter
{
    internal static class Program
    {
        private static readonly ExternalTestContext TestContext = new("YAML Linter", StreamWriter.Null);

        private static async Task<int> Main(string[] _)
        {
            // Sunrise edit start - линтеру нужны метаданные RSI, но не декодированные изображения
            RsiLoadingPatch.Apply();
            GameDataScrounger.NoScrounging = true; // Ugly hack for YAML Linter.
            PoolManager.Startup();
            try
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                var (errors, fieldErrors) = await RunValidation();

                var count = errors.Count + fieldErrors.Count;

                if (count == 0)
                {
                    Console.WriteLine($"No errors found in {(int) stopwatch.Elapsed.TotalMilliseconds} ms.");
                    return 0;
                }

                foreach (var (file, errorHashset) in errors)
                {
                    foreach (var errorNode in errorHashset)
                    {
                        // TODO YAML LINTER Fix inheritance
                        // If a parent/abstract prototype has na error, this will misreport the file name (but with the correct line/column).
                        Console.WriteLine($"::error in {file}({errorNode.Node.Start.Line},{errorNode.Node.Start.Column})  {errorNode.ErrorReason}");
                    }
                }

                foreach (var error in fieldErrors)
                {
                    Console.WriteLine(error);
                }

                Console.WriteLine($"{count} errors found in {(int) stopwatch.Elapsed.TotalMilliseconds} ms.");
                return -1;
            }
            finally
            {
                PoolManager.Shutdown();
                RsiLoadingPatch.Unpatch();
            }
            // Sunrise edit end
        }

        // Sunrise edit start - используем одну пару и проверяем обе стороны одновременно
        private static async Task<(Dictionary<string, HashSet<ErrorNode>> YamlErrors, List<string> FieldErrors)> ValidateInstance(
            RobustIntegrationTest.IntegrationInstance instance)
        {
            var protoMan = instance.ResolveDependency<IPrototypeManager>();
            Dictionary<string, HashSet<ErrorNode>> yamlErrors = default!;
            List<string> fieldErrors = default!;

            await instance.WaitPost(() =>
            {
                var engineErrors = protoMan.ValidateDirectory(new ResPath("/EnginePrototypes"), out var engPrototypes);
                yamlErrors = protoMan.ValidateDirectory(new ResPath("/Prototypes"), out var prototypes);

                // Merge engine & content prototypes
                foreach (var (kind, instances) in engPrototypes)
                {
                    if (prototypes.TryGetValue(kind, out var existing))
                        existing.UnionWith(instances);
                    else
                        prototypes[kind] = instances;
                }

                foreach (var (kind, set) in engineErrors)
                {
                    if (yamlErrors.TryGetValue(kind, out var existing))
                        existing.UnionWith(set);
                    else
                        yamlErrors[kind] = set;
                }

                fieldErrors = protoMan.ValidateStaticFields(prototypes);
            });

            return (yamlErrors, fieldErrors);
        }

        public static async Task<(Dictionary<string, HashSet<ErrorNode>> YamlErrors, List<string> FieldErrors)>
            RunValidation()
        {
            await using var pair = await PoolManager.GetServerClient(testContext: TestContext);
            var clientAssemblies = GetAssemblies(pair.Client);
            var serverAssemblies = GetAssemblies(pair.Server);
            var serverTypes = serverAssemblies.SelectMany(n => n.GetTypes()).Select(t => t.Name).ToHashSet();
            var clientTypes = clientAssemblies.SelectMany(n => n.GetTypes()).Select(t => t.Name).ToHashSet();

            var yamlErrors = new Dictionary<string, HashSet<ErrorNode>>();

            var serverValidation = ValidateInstance(pair.Server);
            var clientValidation = ValidateInstance(pair.Client);
            await Task.WhenAll(serverValidation, clientValidation);

            var serverErrors = await serverValidation;
            var clientErrors = await clientValidation;
            await pair.CleanReturnAsync();

            foreach (var (key, val) in serverErrors.YamlErrors)
            {
                // Include all server errors marked as always relevant
                var newErrors = val.Where(n => n.AlwaysRelevant).ToHashSet();

                // We include sometimes-relevant errors if they exist both for the client & server
                if (clientErrors.YamlErrors.TryGetValue(key, out var clientVal))
                    newErrors.UnionWith(val.Intersect(clientVal));

                // Include any errors that relate to server-only types
                foreach (var errorNode in val)
                {
                    if (errorNode is FieldNotFoundErrorNode fieldNotFoundNode && !clientTypes.Contains(fieldNotFoundNode.FieldType.Name))
                    {
                        newErrors.Add(errorNode);
                    }
                }

                if (newErrors.Count != 0)
                    yamlErrors[key] = newErrors;
            }

            // Next add any always-relevant client errors.
            foreach (var (key, val) in clientErrors.YamlErrors)
            {
                var newErrors = val.Where(n => n.AlwaysRelevant).ToHashSet();

                // Include any errors that relate to client-only types
                foreach (var errorNode in val)
                {
                    if (errorNode is FieldNotFoundErrorNode fieldNotFoundNode
                        && !serverTypes.Contains(fieldNotFoundNode.FieldType.Name))
                    {
                        newErrors.Add(errorNode);
                    }
                }

                if (newErrors.Count == 0)
                    continue;

                if (yamlErrors.TryGetValue(key, out var errors))
                    errors.UnionWith(newErrors);
                else
                    yamlErrors[key] = newErrors;
            }

            // Finally, combine the prototype ID field errors.
            var fieldErrors = serverErrors.FieldErrors
                .Concat(clientErrors.FieldErrors)
                .Distinct()
                .ToList();

            return (yamlErrors, fieldErrors);
        }

        private static Assembly[] GetAssemblies(RobustIntegrationTest.IntegrationInstance instance)
        {
            var refl = instance.ResolveDependency<IReflectionManager>();
            return refl.Assemblies.ToArray();
        }
        // Sunrise edit end
    }
}
