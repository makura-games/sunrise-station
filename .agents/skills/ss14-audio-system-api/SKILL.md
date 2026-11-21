---
name: ss14-audio-system-api
description: Gives a practical catalog of the AudioSystem API in Space Station 14: when to use the Play/Set/Stop/Resolve methods, how to work with predicted audio and filters, and how to use OpenAL EFX (auxiliary/effect/preset) without common mistakes.
---

# AudioSystem: API practice

Use this skill when you need to quickly select the correct AudioSystem method and apply it without regressions :)
Freshness reference: `git blame` with cutoff `2024-02-19`.

## Quick API selection

1. We need a sound “for everyone around the source”: `PlayPvs(sound, uid/coords, params)`.
2. We need a sound “for a specific recipient”: `PlayEntity/PlayGlobal/PlayStatic(..., recipient, ...)`.
3. We need predicted UX without duplicates: `PlayPredicted(sound, source/coords, user, params)`.
4. We need runtime flow control: `SetState`, `SetPlaybackPosition`, `Stop`, `IsPlaying`.
5. You need a map/grid special sound: `SetMapAudio`, `SetGridAudio`.
6. You need a reverb/filter effect: `CreateEffect + CreateAuxiliary + SetEffectPreset + SetEffect + SetAuxiliary`.
7. Need volume/tone/range setting: `AudioParams.With*` and/or `SetVolume/SetGain`.

## API by group

### 1) Resolve and utilities

1. `ResolveSound(SoundSpecifier)` - pins a specific track from the collection.
2. `GetAudioPath(ResolvedSoundSpecifier?)` - gets the real path.
3. `GetAudioLength(...)` - duration for seek/timers.
4. `GetAudioDistance(length)` - takes into account Z-offset.
5. `GainToVolume/VolumeToGain` - translation between gain and dB.

### 2) Playback API (basis)

1. `PlayGlobal(...)` - non-positional sound.
2. `PlayEntity(...)` - the sound follows the entity.
3. `PlayStatic(...)` - sound in fixed coordinates.
4. `PlayPvs(...)` - shorthand for the PVS audience.
5. All groups have overloads under:
`SoundSpecifier`/`ResolvedSoundSpecifier`, `Filter`, `ICommonSession`, `EntityUid recipient`.

### 3) Predicted API

1. `PlayPredicted(sound, source, user, params)`.
2. `PlayPredicted(sound, coords, user, params)`.
3. `PlayLocal(...)` - local predicted-alias.

### 4) Control of an already playing thread

1. `SetState(stream, Playing/Paused/Stopped)`.
2. `SetPlaybackPosition(stream, seconds)`.
3. `SetVolume(stream, dB)` / `SetGain(stream, gain)`.
4. `Stop(stream)`.
5. `IsPlaying(stream)`.

### 5) Spatial map/grid modes

1. `SetMapAudio(audioEntity)` - map-wide sound.
2. `SetGridAudio(audioEntity)` — grid center, tuned distance, `NoOcclusion`.

### 6) Stream API (client runtime)

1. `PlayGlobal(AudioStream, ...)`.
2. `PlayEntity(AudioStream, ...)`.
3. `PlayStatic(AudioStream, ...)`.
4. `LoadStream<T>(entity, stream)` — loading source from stream-type.

### 7) OpenAL Effects API (EFX)

1. `CreateEffect()` - creates an effect entity.
2. `CreateAuxiliary()` - creates an auxiliary slot entity.
3. `SetEffectPreset(effect, presetProtoOrReverb)` - applies a set of parameters.
4. `SetEffect(aux, effect)` - attaches effect to slot.
5. `SetAuxiliary(audio, aux)` - connects the slot to the source.
6. Shortcut: `SetEffect(audioUid, audioComp, presetId)`.

## Patterns

1. For player interactions, always pass `user` and use `PlayPredicted`.
2. For long music/stateful SFX, store stream `EntityUid?` and control via `SetState/Stop`.
3. For wobbly/large objects (shuttles, platforms) do `PlayPvs -> SetGridAudio`.
4. For map events (timers, global phases) do `PlayPvs -> SetMapAudio`.
5. For rich-acoustics, first assemble an EFX chain, then connect it to the active stream.
6. For one-off sounds with variation, use `AudioParams.WithVariation(...)`.
7. For “quieter/louder in runtime” prefer `SetVolume`; for linear coefficients - `SetGain`.

## Anti-patterns

1. Pull `PlayPvs` in the predicted code where `PlayPredicted` is needed.
2. Use old TODO zones as a template for the new API.
3. Rely on server `LoadStream<T>` as a work contract.
4. Add effects until the real source is ready, without taking into account the life cycle.
5. Mix map/grid semantics manually instead of `SetMapAudio/SetGridAudio`.
6. Ignore the `AudioState` state and try to “cure” the flow only by deleting the entity.

## OpenAL effects: practical minimum

1. Reverb parameters live in `AudioEffectComponent` (`Density`, `Decay*`, `Echo*`, `Modulation*`, `HF/LF*`).
2. `AudioAuxiliaryComponent` holds the link to the effect.
3. `AudioComponent.Auxiliary` connects slot to a specific source.
4. Occlusion in EFX mode uses lowpass; without EFX it degrades into gain-fallback.

## Code examples

### 1) Predicted sound from interaction

```csharp
// The user already hears locally; the server will exclude it from the repeat event.
_audio.PlayPredicted(ent.Comp.UseSound, ent.Owner, args.User, AudioParams.Default.WithVariation(0.25f));
```

### 2) Station music/event only for the right audience

```csharp
// The server itself creates a station filter + PVS and sends out a network event.
var spec = _audio.ResolveSound(sound);
_globalSound.PlayGlobalOnStation(sourceUid, spec, AudioParams.Default.WithVolume(-8f));
```

### 3) Ping-aware seek for synchronous playback

```csharp
// Compensation for network delay when rewinding the current track.
var offset = session.Channel.Ping * 1.5f / 1000f;
Audio.SetPlaybackPosition(streamUid, requestedTime + offset);
```

### 4) Grid audio for FTL phase

```csharp
// We tie the shuttle sound to grid semantics, and not to a local point.
var audio = _audio.PlayPvs(startupSound, shuttleUid);
_audio.SetGridAudio(audio);
```

### 5) Complete EFX chain

```csharp
// We created an effect + slot, applied a preset and connected it to a specific audio.
var effect = _audio.CreateEffect();
var aux = _audio.CreateAuxiliary();

_audio.SetEffectPreset(effect.Entity, effect.Component, presetPrototype);
_audio.SetEffect(aux.Entity, aux.Component, effect.Entity);
_audio.SetAuxiliary(soundUid, soundComp, aux.Entity);
```

### 6) Thread state management

```csharp
// Pause/resume/stop - via state API.
Audio.SetState(streamUid, AudioState.Paused);
Audio.SetState(streamUid, AudioState.Playing);
Audio.SetState(streamUid, AudioState.Stopped);
```

## Rule of application

1. By default, select `Fresh-Use` methods from the reference directory.
2. Use `Legacy-Compat` only when clearly necessary and with tests.
3. Do not use `Risk/TODO` as a reference point for a new API design.
4. Check any audio API refactoring for prediction, PVS and EFX fallback at the same time.

Keep the API layer predictable: the right overload and the right recipient are more important than a “short” call 😅
