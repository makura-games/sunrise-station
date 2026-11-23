#nullable enable
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._Sunrise.Patches;

/// <summary>
/// Измеряет время направленной рассылки событий и печатает разницу за один тест.
/// </summary>
internal static class EventTimingSummaryPatch
{
    private static readonly ConcurrentDictionary<string, long> _eventTotals = new();
    private static readonly ConcurrentDictionary<string, long> _componentTotals = new();
    private static readonly List<IDisposable> _hooks = [];

    private static Dictionary<string, long> _eventSnapshot = [];
    private static Dictionary<string, long> _componentSnapshot = [];
    private static int _applied;

    internal static void Apply()
    {
        if (Interlocked.Exchange(ref _applied, 1) != 0)
            return;

        var eventBusType = typeof(EntityManager).Assembly.GetType("Robust.Shared.GameObjects.EntityEventBus");
        if (eventBusType == null)
        {
            TestContext.Error.WriteLine("[EventTimingSummaryPatch] EntityEventBus type was not found; event profiling is disabled.");
            Interlocked.Exchange(ref _applied, 0);
            return;
        }

        var dispatch = eventBusType.GetMethod("EntDispatch", BindingFlags.Instance | BindingFlags.NonPublic);
        if (dispatch == null)
        {
            TestContext.Error.WriteLine("[EventTimingSummaryPatch] EntDispatch method was not found; event profiling is disabled.");
            Interlocked.Exchange(ref _applied, 0);
            return;
        }

        _hooks.Add(new ILHook(dispatch, InjectEntDispatchTiming));
    }

    internal static void Unpatch()
    {
        foreach (var hook in _hooks)
        {
            hook.Dispose();
        }

        _hooks.Clear();
        _eventTotals.Clear();
        _componentTotals.Clear();
        _eventSnapshot.Clear();
        _componentSnapshot.Clear();
        Interlocked.Exchange(ref _applied, 0);
    }

    internal static Task TakeSnapshot()
    {
        _eventSnapshot = _eventTotals.ToDictionary(pair => pair.Key, pair => pair.Value);
        _componentSnapshot = _componentTotals.ToDictionary(pair => pair.Key, pair => pair.Value);
        return Task.CompletedTask;
    }

    internal static async Task PrintTop10(TextWriter output)
    {
        await PrintTop10(output, "event dispatches", _eventTotals, _eventSnapshot);
        await PrintTop10(output, "component subscriptions", _componentTotals, _componentSnapshot);
    }

    private static void InjectEntDispatchTiming(ILContext il)
    {
        var getTimestamp = typeof(System.Diagnostics.Stopwatch).GetMethod(
            nameof(System.Diagnostics.Stopwatch.GetTimestamp),
            BindingFlags.Public | BindingFlags.Static);
        var recordTiming = typeof(EventTimingSummaryPatch).GetMethod(
            nameof(RecordTiming),
            BindingFlags.NonPublic | BindingFlags.Static);
        if (getTimestamp == null || recordTiming == null)
            return;

        var startTicks = new VariableDefinition(il.Import(typeof(long)));
        il.Body.Variables.Add(startTicks);
        il.Body.InitLocals = true;

        var cursor = new ILCursor(il);
        while (cursor.TryGotoNext(
                   MoveType.Before,
                   instruction => instruction.OpCode == OpCodes.Callvirt
                       && instruction.Operand is Mono.Cecil.MethodReference method
                       && method.Name == "Invoke"
                       && method.DeclaringType.Name.Contains("DirectedEventHandler")))
        {
            var invoke = cursor.Next;
            var componentLoad = invoke?.Previous?.Previous;
            if (componentLoad == null || !TryGetLocal(componentLoad, out var componentLocal))
            {
                cursor.Goto(invoke!.Next, MoveType.Before);
                continue;
            }

            cursor.Emit(OpCodes.Call, getTimestamp);
            cursor.Emit(OpCodes.Stloc, startTicks);

            cursor.Goto(invoke!.Next, MoveType.Before);
            cursor.Emit(OpCodes.Ldarg_2);
            cursor.Emit(OpCodes.Ldloc, componentLocal);
            cursor.Emit(OpCodes.Ldloc, startTicks);
            cursor.Emit(OpCodes.Call, recordTiming);

            cursor.Goto(invoke.Next, MoveType.After);
        }
    }

    private static bool TryGetLocal(Instruction instruction, out VariableDefinition local)
    {
        local = null!;
        if (instruction.OpCode.Code is not (Code.Ldloc or Code.Ldloc_S))
            return false;

        local = (VariableDefinition) instruction.Operand;
        return true;
    }

    private static void RecordTiming(Type eventType, IComponent component, long startTicks)
    {
        var elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - startTicks;
        var eventName = eventType.Name;
        var componentName = component.GetType().Name;

        _eventTotals.AddOrUpdate(eventName, elapsed, (_, previous) => previous + elapsed);
        _componentTotals.AddOrUpdate($"{componentName} / {eventName}", elapsed, (_, previous) => previous + elapsed);
    }

    private static async Task PrintTop10(
        TextWriter output,
        string title,
        ConcurrentDictionary<string, long> current,
        Dictionary<string, long> snapshot)
    {
        if (current.IsEmpty)
            return;

        var deltas = current
            .Select(pair => (Name: pair.Key, Delta: pair.Value - snapshot.GetValueOrDefault(pair.Key)))
            .Where(entry => entry.Delta > 0)
            .OrderByDescending(entry => entry.Delta)
            .Take(10)
            .ToList();

        if (deltas.Count == 0)
            return;

        await output.WriteLineAsync($"  ┌─ Top 10 {title}");
        for (var i = 0; i < deltas.Count; i++)
        {
            var milliseconds = deltas[i].Delta * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            await output.WriteLineAsync($"  │ {i + 1,2}. {deltas[i].Name,-55} {milliseconds,8:F2} ms");
        }

        await output.WriteLineAsync("  └" + new string('─', 70));
    }
}
