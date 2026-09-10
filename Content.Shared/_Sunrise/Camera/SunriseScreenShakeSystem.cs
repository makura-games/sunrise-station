using System.Numerics;
using Content.Shared.Camera;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Noise;
using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.Camera;

public sealed class SunriseScreenShakeSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const float MaxOffset = 0.15f;
    private const float MaxRotationDegrees = 20f;

    private readonly FastNoiseLite _translationXNoise = CreateNoise(67);
    private readonly FastNoiseLite _translationYNoise = CreateNoise(68);
    private readonly FastNoiseLite _rotationNoise = CreateNoise(487);
    private readonly List<SunriseScreenShakeCommand> _expiredCommands = [];

    private EntityQuery<EyeComponent> _eyeQuery;

    public override void Initialize()
    {
        base.Initialize();

        _eyeQuery = GetEntityQuery<EyeComponent>();

        SubscribeLocalEvent<SunriseScreenShakeComponent, GetEyeOffsetEvent>(OnGetEyeOffset);
        SubscribeLocalEvent<SunriseScreenShakeComponent, GetEyeRotationEvent>(OnGetEyeRotation);
        SubscribeLocalEvent<SunriseScreenShakeComponent, EntityUnpausedEvent>(OnEntityUnpaused);
    }

    private void OnGetEyeOffset(Entity<SunriseScreenShakeComponent> ent, ref GetEyeOffsetEvent args)
    {
        var intensity = _configuration.GetCVar(CCVars.ScreenShakeIntensity);
        if (intensity <= 0f)
            return;

        var realTime = (float) _timing.RealTime.TotalMilliseconds;
        var offset = Vector2.Zero;

        foreach (var command in ent.Comp.Commands)
        {
            if (command.Translational is not { } parameters)
                continue;

            var trauma = CalculateCurrentTrauma(parameters, command.Start) * intensity;
            if (trauma <= 0f)
                continue;

            var commandTime = (float) command.Start.TotalMilliseconds;
            _translationXNoise.SetFrequency(parameters.Frequency);
            _translationYNoise.SetFrequency(parameters.Frequency);

            offset.X += MaxOffset * trauma * _translationXNoise.GetNoise(realTime, commandTime);
            offset.Y += MaxOffset * trauma * _translationYNoise.GetNoise(realTime, commandTime);
        }

        args.Offset += offset;
    }

    private void OnGetEyeRotation(Entity<SunriseScreenShakeComponent> ent, ref GetEyeRotationEvent args)
    {
        var intensity = _configuration.GetCVar(CCVars.ScreenShakeIntensity);
        if (intensity <= 0f)
            return;

        var realTime = (float) _timing.RealTime.TotalMilliseconds;
        var rotation = Angle.Zero;

        foreach (var command in ent.Comp.Commands)
        {
            if (command.Rotational is not { } parameters)
                continue;

            var trauma = CalculateCurrentTrauma(parameters, command.Start) * intensity;
            if (trauma <= 0f)
                continue;

            _rotationNoise.SetFrequency(parameters.Frequency);
            var angle = MaxRotationDegrees * trauma * _rotationNoise.GetNoise(
                realTime,
                (float) command.Start.TotalMilliseconds);
            rotation += Angle.FromDegrees(angle);
        }

        args.Rotation += rotation;
    }

    private void OnEntityUnpaused(Entity<SunriseScreenShakeComponent> ent, ref EntityUnpausedEvent args)
    {
        var shiftedCommands = new HashSet<SunriseScreenShakeCommand>();
        foreach (var command in ent.Comp.Commands)
        {
            shiftedCommands.Add(command with
            {
                Start = command.Start + args.PausedTime,
                CalculatedEnd = command.CalculatedEnd + args.PausedTime,
            });
        }

        ent.Comp.Commands = shiftedCommands;
        Dirty(ent);
    }

    /// <summary>
    /// Добавляет владельцу сущности плавную поступательную и/или вращательную тряску.
    /// </summary>
    public void Shake(
        EntityUid uid,
        SunriseScreenShakeParameters? translational,
        SunriseScreenShakeParameters? rotational)
    {
        if (translational is null && rotational is null)
            return;

        if (!_eyeQuery.HasComp(uid))
            return;

        var start = _timing.CurTime;
        var end = CalculateEndTime(translational, rotational, start);
        if (end <= start)
            return;

        var component = EnsureComp<SunriseScreenShakeComponent>(uid);
        var command = new SunriseScreenShakeCommand(translational, rotational, start, end);
        if (!component.Commands.Add(command))
            return;

        Dirty(uid, component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SunriseScreenShakeComponent, EyeComponent>();
        while (query.MoveNext(out var uid, out var component, out _))
        {
            _expiredCommands.Clear();

            foreach (var command in component.Commands)
            {
                if (_timing.CurTime >= command.CalculatedEnd)
                    _expiredCommands.Add(command);
            }

            if (_expiredCommands.Count == 0)
                continue;

            foreach (var command in _expiredCommands)
                component.Commands.Remove(command);

            if (component.Commands.Count == 0)
                RemCompDeferred<SunriseScreenShakeComponent>(uid);
            else
                Dirty(uid, component);
        }
    }

    private float CalculateCurrentTrauma(SunriseScreenShakeParameters parameters, TimeSpan start)
    {
        var elapsed = _timing.CurTime - start;
        if (elapsed < TimeSpan.Zero)
            return 0f;

        var elapsedSeconds = (float) elapsed.TotalSeconds;
        return parameters.Trauma - elapsedSeconds * elapsedSeconds * parameters.DecayRate;
    }

    private static TimeSpan CalculateEndTime(
        SunriseScreenShakeParameters? translational,
        SunriseScreenShakeParameters? rotational,
        TimeSpan start)
    {
        var translationalDuration = CalculateDuration(translational);
        var rotationalDuration = CalculateDuration(rotational);
        return start + TimeSpan.FromSeconds(MathF.Max(translationalDuration, rotationalDuration));
    }

    private static float CalculateDuration(SunriseScreenShakeParameters? parameters)
    {
        if (parameters is not { Trauma: > 0f, DecayRate: > 0f })
            return 0f;

        return MathF.Sqrt(parameters.Trauma / parameters.DecayRate);
    }

    private static FastNoiseLite CreateNoise(int seed)
    {
        var noise = new FastNoiseLite(seed);
        noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        return noise;
    }
}
