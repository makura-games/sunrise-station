---
name: ss14-audio-system-core
description: Explains the AudioSystem architecture in Space Station 14 at the shared/server/client and OpenAL levels: audio entity lifecycle, PVS filtering, occlusion, streaming and EFX chain. Use when you need to understand the internal design of a system before expanding or debugging.
---

# AudioSystem: Architecture and Internals

Use this skill as an architectural playbook for AudioSystem :)
Keep your focus on fresh code and check its relevance through `git blame` (cutoff: `2024-02-19`).

## Source of truth

1. The codebase is the primary source of truth.
2. Documentation - secondary: for terms, intent and hypothesis testing.
3. Any site older than two years or with obvious TODO/problematic comments should not be included in the rules as a “standard”.

## AudioSystem mental model

1. `SharedAudioSystem` specifies the contract: creation/stopping, state, positioning, audience routing.
2. `AudioComponent` — network state of the audio stream (state + params + filtering + binding to auxiliary).
3. The server creates audio entities and decides who to replicate them to (filter/PVS/override), but does not play the actual audio.
4. The client creates a real `IAudioSource`, applies `AudioParams`, updates the position/occlusion and manages the playback.
5. For positional sound, the key loop is: calculate position -> check distance -> evaluate occlusion -> update gain/velocity.
6. Effects come in a separate chain: `AudioSource -> Auxiliary Slot -> AudioEffect (EAX Reverb)`.
7. When EFX is enabled, occlusion works through a lowpass filter; without EFX - through fallback by gain.
8. For streams, “instant processing after start” is important so that the sound does not live for one tick with an incorrect position/occlusion.

## Layer scheme

1. Shared layer:
`ResolveSound`, `SetupAudio`, `SetState`, `SetPlaybackPosition`, `SetMapAudio`, `SetGridAudio`, `Stop`.
2. Server layer:
selection of recipients, `PlayPvs/PlayEntity/PlayStatic/PlayGlobal`, PVS override for map/grid audio.
3. Client layer:
`FrameUpdate`, `ProcessStream`, `GetOcclusion`, audio-limit, stream playback.
4. OpenAL layer:
`AudioManager`, `BaseAudioSource`, `AudioEffect`, `AuxiliaryAudio`.

## Patterns

1. For locally predicted interactions, use `PlayPredicted(..., user)` instead of “pure” `PlayPvs`.
2. For map/grid special sounds, use `SetMapAudio`/`SetGridAudio`, and not manual emulation through random filters.
3. For streams launched in the middle of a tick, immediately recalculate the spatial state (position/occlusion/velocity).
4. For complex custom acoustics, connect `ProcessStreamOverride`/`GetOcclusionOverride` with one subscriber.
5. For OpenAL effects, assemble a bunch: `CreateEffect -> CreateAuxiliary -> SetEffect -> SetAuxiliary`.
6. For repeated short SFX, consider the limit concurrent sound, otherwise you can “eat” the source budget.
7. For long-running music/event sound, store stream `EntityUid?` and manage the state via `SetState`/`Stop`.
8. For global station/card music, create an audience through a server filter, not on the client.

## Anti-patterns

1. Rely on the `LoadStream` sections in the server implementation as a working API path.
2. Copy old zones with TODO about range/PVS/Transform as a template for new logic.
3. Attach an effect through a race timer without source life cycle guarantees (fragile race 😬).
4. Mutate the volume/gain into a tight loop without guard checks and without understanding EFX fallback behavior.
5. Use the same implementation for predicted and non-predicted cases without explicit user.
6. Consider docs as the “truth of behavior” where the code has already gone ahead.

## OpenAL/EFX: what is important to understand

1. `AudioManager` opens the OpenAL device/context and defines support for `ALC_EXT_EFX`.
2. `AudioSource` controls the AL source (`Gain`, `Pitch`, `Position`, `Velocity`, `SecOffset`).
3. `AuxiliaryAudio` — effect slot into which `AudioEffect` is attached.
4. `AudioEffect` maps reverb fields (`Density`, `Decay*`, `Echo*`, `Modulation*`, `HF/LF references`) in EFX parameters.
5. For EFX occlusion, lowpass (`LowpassGain`, `LowpassGainHF`) is used, otherwise gain-fallback.

## Code examples

### 1) Stream + immediate spatial recalculation

```csharp
// After the start of the stream in the middle of a tick, we immediately recalculate spatial,
// otherwise one tick you can hear the sound in the wrong position.
var playing = CreateAndStartPlayingStream(audioParams, specifier, stream);
SetCoordinates(playing.Entity, new EntityCoordinates(sourceEntity, Vector2.Zero));

ProcessStream(
    playing.Entity,
    playing.Component,
    Transform(playing.Entity),
    GetListenerCoordinates());
```

### 2) Grid audio for large moving objects

```csharp
// We mark the sound as grid-audio: grid center + extended range + no occlusion.
var stream = PlayPvs(startupSound, shuttleUid);
SetGridAudio(stream);
```

### 3) Ping compensation for playback position

```csharp
// To synchronize the music stream, we take into account the network delay of the session.
var offset = session.Channel.Ping * 1.5f / 1000f;
SetPlaybackPosition(jukeboxStream, requestedSongTime + offset);
```

### 4) EFX reverb chain

```csharp
// Create an effect + auxiliary, apply a preset and connect to the source.
var effect = CreateEffect();
var aux = CreateAuxiliary();

SetEffectPreset(effect.Entity, effect.Component, presetProto);
SetEffect(aux.Entity, aux.Component, effect.Entity);
SetAuxiliary(audioUid, audioComp, aux.Entity);
```

### 5) Server predicted-flow without duplicates for the initiator

```csharp
// The server sends audio via PVS, but excludes the initiator,
// because the initiator has already predicted locally.
var audio = PlayPvs(ResolveSound(sound), sourceUid, audioParams);
if (audio != null)
    audio.Value.Component.ExcludedEntity = user;
```

## Extension rule

1. First build new mechanics on fresh entry points (`stream overloads`, `SetGridAudio/SetMapAudio`, override hooks).
2. If you have to touch the legacy zone, first record the risk in `references/rejected-snippets.md`.
3. Confirm any audio optimization by profiling and listening in a real round.
4. For effects, first check EFX support, then design fallback behavior.

Think of AudioSystem as a network contract + client DSP pipeline, not as a “play file” 🎧
