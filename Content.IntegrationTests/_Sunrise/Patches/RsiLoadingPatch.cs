using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MonoMod.RuntimeDetour;
using Robust.Shared.Maths;
using Robust.Shared.Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Content.IntegrationTests._Sunrise.Patches;

/// <summary>
/// Подменяет декодирование изображений RSI заготовками правильного размера.
/// Интеграционные тесты проверяют метаданные и поведение сущностей, а не отрисовку кадров.
/// </summary>
public static class RsiLoadingPatch
{
    private const string LoadRealTexturesEnvironmentVariable = "SUNRISE_TEST_LOAD_TEXTURES";
    private const string TexturePathPrefix = "/Textures/";

    private static readonly HashSet<string> RealImageRsiPaths = new(StringComparer.Ordinal)
    {
        "Effects/clicktest.rsi",
    };

    private static Hook _hook;

    private delegate Image<Rgba32>[] LoadImagesDelegate(
        object metadata,
        object configuration,
        Func<string, Stream> openStream);

    public static void Apply()
    {
        if (_hook != null || Environment.GetEnvironmentVariable(LoadRealTexturesEnvironmentVariable) == "1")
            return;

        var rsiLoadingType = typeof(RSILoadException).Assembly.GetType("Robust.Shared.Resources.RsiLoading");
        if (rsiLoadingType == null)
        {
            TestContext.Error.WriteLine("[RsiLoadingPatch] RsiLoading type was not found; real textures will be loaded.");
            return;
        }

        MethodInfo original = null;
        foreach (var method in rsiLoadingType.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (method.Name == "LoadImages" && method.GetParameters().Length == 3)
            {
                original = method;
                break;
            }
        }

        if (original == null)
        {
            TestContext.Error.WriteLine("[RsiLoadingPatch] LoadImages method was not found; real textures will be loaded.");
            return;
        }

        _hook = new Hook(original, LoadImagesReplacement);
    }

    public static void Unpatch()
    {
        _hook?.Dispose();
        _hook = null;
    }

    private static Image<Rgba32>[] LoadImagesReplacement(
        LoadImagesDelegate original,
        object metadata,
        object configuration,
        Func<string, Stream> openStream)
    {
        var rsiPath = TryGetRsiPath(openStream);
        if (rsiPath == null || RealImageRsiPaths.Contains(rsiPath))
            return original(metadata, configuration, openStream);

        var metadataType = metadata.GetType();
        var frameSize = (Vector2i) metadataType.GetField("Size")!.GetValue(metadata)!;
        var states = (Array) metadataType.GetField("States")!.GetValue(metadata)!;
        var images = new Image<Rgba32>[states.Length];

        for (var i = 0; i < states.Length; i++)
        {
            var state = states.GetValue(i)!;
            var delays = (float[][]) state.GetType().GetField("Delays")!.GetValue(state)!;
            var totalFrames = 0;
            foreach (var directionDelays in delays)
            {
                totalFrames += directionDelays.Length;
            }

            var image = new Image<Rgba32>(frameSize.X, Math.Max(1, totalFrames) * frameSize.Y);
            image.Mutate(context => context.BackgroundColor(SixLabors.ImageSharp.Color.White));
            images[i] = image;
        }

        return images;
    }

    /// <summary>
    /// Получает путь RSI из замыкания функции открытия кадров.
    /// Если устройство замыкания изменилось, возвращает <see langword="null"/> и сохраняет настоящую загрузку.
    /// </summary>
    private static string TryGetRsiPath(Func<string, Stream> openStream)
    {
        var target = openStream.Target;
        if (target == null)
            return null;

        foreach (var closureField in target.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var captured = closureField.GetValue(target);
            if (captured == null)
                continue;

            var pathField = captured.GetType().GetField("Path", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var pathValue = pathField?.GetValue(captured);
            if (pathValue == null)
                continue;

            var canonicalPathField = pathValue.GetType().GetField("CanonPath", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (canonicalPathField?.GetValue(pathValue) is not string canonicalPath)
                continue;

            return canonicalPath.StartsWith(TexturePathPrefix, StringComparison.Ordinal)
                ? canonicalPath[TexturePathPrefix.Length..]
                : canonicalPath;
        }

        return null;
    }
}
