---
name: ss14-netcode
description: Architecture guide for networking in Space Station 14 — Lidgren integration, NetManager abstraction, message system, game state synchronization, PVS, network events, and component networking
---

# SS14 network architecture

## Stack overview

The SS14 networking stack consists of several layers of abstraction, from low-level transport to game state synchronization logic:

```
┌─────────────────────────────────────────────┐
│ Content (game systems) │
│  RaiseNetworkEvent / ComponentState / Dirty  │
├─────────────────────────────────────────────┤
│         ServerGameStateManager              │
│         ClientGameStateManager              │
│         GameStateProcessor                  │
├─────────────────────────────────────────────┤
│              PVS System                     │
│ (filtering the visibility of entities) │
├─────────────────────────────────────────────┤
│       NetMessage / MsgState / MsgGroups     │
│ (typed messages with serialization)│
├─────────────────────────────────────────────┤
│            NetManager                       │
│ (wrapper over Lidgren, control │
│ channels, packet dispatch) │
├─────────────────────────────────────────────┤
│     Lidgren (NetPeer/NetServer/NetClient)   │
│          UDP + reliability layer            │
└─────────────────────────────────────────────┘
```

## Lidgren: transport layer

SS14 uses the **Lidgren** library for UDP transport. The engine does not use Lidgren directly in the game code - all work is done through the `INetManager` and `NetMessage` abstractions.

### Lidgren configuration

The engine configures Lidgren via CVars:

- **MTU** - Maximum Transmission Unit. Packets larger than MTU are fragmented. Default ~1408 bytes
- **Simulation of network problems** - for testing:
  - `net.fakeloss` — percentage of lost packets (0.0–1.0)
  - `net.fakelagmin` — minimum delay in seconds
  - `net.fakelagrand` - random additional delay
  - `net.fakeduplicates` — chance of packet duplication
- **Buffers** — `net.sendbuffersize`, `net.receivebuffersize`
- **AppIdentifier** - string to identify the protocol (via `CVars.NetLidgrenAppIdentifier`)

### Time synchronization

When initialized, the engine synchronizes `NetTime` Lidgren with its own `RealTime`:

```csharp
NetTime.SetNow(_timing.RealTime.TotalSeconds);
```

This is critical for packet timings to work correctly.

## NetMessage: typed message system

All network messages are inherited from the abstract class `NetMessage`.

### Message groups (MsgGroups)

Each message belongs to a group that determines the default delivery method:

| Group | Delivery | Destination |
|--------|----------|------------|
| `Core` | ReliableUnordered | Connections, disconnections, ticks |
| `Entity` | Unreliable Game state synchronization |
| `String` | ReliableOrdered | Chat, text messages |
| `Command` | ReliableUnordered | Commands client → server |
| `EntityEvent` | ReliableOrdered | ECS events between server and client |

### Lidgren delivery methods

- **Unreliable** - no guarantee of delivery, no order. The fastest
- **ReliableUnordered** — delivery guarantee, without order guarantee
- **ReliableOrdered** - guarantee of delivery and order (within the sequence channel)

### Sequence Channels

Lidgren supports up to 32 channels for ordered messages. Channels 16+ are reserved for internal engine needs. Messages in different channels are ordered independently.

### Registration and processing

```csharp
// Registering a message with a handler
_networkManager.RegisterNetMessage<MsgStateAck>(HandleStateAck);

// Registration without handler (for sending only)
_networkManager.RegisterNetMessage<MsgState>();
```

## NetManager: network management

`NetManager` is the central point of the network subsystem. It implements both interfaces: `IClientNetManager` and `IServerNetManager`.

### Main processing cycle

The `ProcessPackets()` method is called every frame and does:

1. Reads all incoming messages from Lidgren peers
2. Classifies by `NetIncomingMessageType`:
   - `Data` → deserializes to `NetMessage`, calls the registered callback
   - `StatusChanged` → handles connections/disconnections
   - `VerboseDebugMessage`, `WarningMessage`, `ErrorMessage` → logs
3. Updates Prometheus metrics (sent/received packets, bytes, resends)
4. Recycles used Lidgren buffers

### StringTable

To save traffic, message type names are passed as numeric IDs via `StringTable`. When connecting, the server and client synchronize the mapping table.

### Channels (NetChannel)

Each connection is represented by `NetChannel`, which stores:
- Lidgren `NetConnection`
- User ID (`NetUserId`)
- Ping
- Authentication status

## Synchronizing the game state (GameState)

### GameState structure

`GameState` — a snapshot of the game state at a specific tick. Contains:

- **EntityStates** — changed states of entity components
- **PlayerStates** — player session states
- **EntityDeletions** — deleted entities
- **FromSequence / ToSequence** — tick range (delta state)
- **LastProcessedInput** — the last client input processed by the server

### Delta states

The server sends the **delta** between two ticks, rather than the full state each time. `FromSequence = 0` means full status (after connection or on error).

### Compression and reliability (MsgState)

The `MsgState` class implements smart dispatch logic:

- **ZStd compression** - states > 256 bytes are compressed
- **Adaptive reliability** - if the resulting size exceeds the Lidgren MTU (~1408 bytes), the message is sent as Reliable. Otherwise - Unreliable
- **Forced reliability** - `ForceSendReliably` for states that the client must receive

### Flow server → client

1. **Server:** `ServerGameStateManager.SendGameStateUpdate()` → `PvsSystem.SendGameStates(players)`
2. PVS calculates visible entities for each player
3. An individual `GameState` is formed for each player
4. State is serialized, compressed, sent as `MsgState`
5. **Client:** receives `MsgState`, deserializes, places in `GameStateProcessor`
6. The client responds with `MsgStateAck` confirmation

### Client state processing

`GameStateProcessor` buffers received states and releases them for use:

- **State Buffer** - stores several states in advance for smoothing
- **Target buffer size** — target buffer size, affects latency vs smoothness
- **Tick timing adjustment** — the client slightly speeds up/slows down its tick to synchronize with the server

### Query full status

If there is an error (missing entity metadata, desynchronization), the client requests the full state via `MsgStateRequestFull`. The server responds with a state of `FromSequence = 0`.

## Potentially Visible Set (PVS)

PVS is a server-side optimization system that determines which entities each client sees.

### Operating principle

- The world is divided into **chunks**
- The server tracks the position of each player
- Entities within a certain radius enter the player's PVS
- Only visible entity data is sent to the client

### Visibility Lifecycle

1. **Entity enters PVS** - the client receives the full state, the `Detached` flag is cleared
2. **Entity in PVS** - client receives delta updates
3. **The entity leaves PVS** - the server sends `MsgStateLeavePvs`, the client sets the flag `MetaDataFlags.Detached`

### Detached entities

When an entity exits PVS:
- It **is not deleted** - remains in the client’s memory
- The flag `MetaDataFlags.Detached` is set
- The entity moves to “null-space” (removed from broadphase)
- When re-entering PVS, the flag is removed, the entity returns to its place

### PVS Overrides

For entities that should be visible always or to specific players:

```csharp
// Visible to all clients, regardless of distance
_pvs.AddGlobalOverride(entityUid);

// Visible to a specific session
_pvs.AddSessionOverride(entityUid, session);
```

Typical applications: player UI entities, global controllers, objects on the character’s hands.

## Network Events

### Two types of events

SS14 has **local** and **online** events. These are fundamentally different mechanisms:

```csharp
// Local event - only in the current process
RaiseLocalEvent(uid, new MyLocalEvent());
SubscribeLocalEvent<MyComponent, MyLocalEvent>(OnMyEvent);

// Network event - sent over the network
RaiseNetworkEvent(new MyNetEvent());
SubscribeNetworkEvent<MyNetEvent>(OnNetEvent);
```

### Server Validation

**All events from the client to the server must be validated.** The client can send any data:

```csharp
// ❌ Dangerous - no validation
private void OnClientEvent(MyEvent ev, EntitySessionEventArgs args)
{
    DoAction(ev.TargetEntity); // The client could have spoofed the TargetEntity!
}

// ✅ Safe - with validation
private void OnClientEvent(MyEvent ev, EntitySessionEventArgs args)
{
    if (!HasComp<MyComponent>(ev.TargetEntity))
        return;
    if (!_interaction.InRangeUnobstructed(args.SenderSession, ev.TargetEntity))
        return;
    DoAction(ev.TargetEntity);
}
```

Validation must occur on the server after receiving the message and on the client before it is sent.
Validation code must be in a shared system/helper class to prevent duplication of logic!

### Sending patterns

```csharp
// Server → all clients
RaiseNetworkEvent(new MyEvent());

// Server → specific client
RaiseNetworkEvent(new MyEvent(), session);

// Server → everyone except one
var filter = Filter.Broadcast().RemovePlayerByAttachedEntity(uid);
RaiseNetworkEvent(new MyEvent(), filter);
```

## EntityUid vs NetEntity

SS14 has two entity identification systems:

| | EntityUid | NetEntity |
|---|---|---|
| Where is it used | Locally in progress | When transmitted over a network |
| Stability | Different on client and server | Same everywhere |
| Storage | In components | For transmission only |

### Conversion

```csharp
// EntityUid → NetEntity (to be sent over the network)
var netEntity = GetNetEntity(uid);

// NetEntity → EntityUid (when received from the network)
var uid = GetEntity(netEntity);
```

### Rules

- **Components** store `EntityUid`, not `NetEntity`
- During network synchronization (`[AutoNetworkedField]`), conversion occurs automatically
- For manual networking, use `GetNetEntity()`/`GetEntity()` when serializing/deserializing

## Component Networking: network synchronization of components

### Automatic synchronization

This is the basic and recommended method. Described in detail in the skill **SS14 ECS Components**:

```csharp
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MyComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Value = 1f;
}
```

### Key Point: Dirty()

After changing component data in code, it is **required** to call `Dirty()`:

```csharp
comp.Value = newValue;
Dirty(uid, comp); // Without this, the client will NOT receive the update!
```

`Dirty()` marks the component as changed. The PVS system will include it in the next `GameState` to send to clients.

> **⚠️ Common mistake:** forgetting `Dirty()` after changing a component. The data will change on the server, but the client will not receive the update, which will lead to desynchronization.

### SendOnlyToOwner

For data that only the "owner" of the entity should see (such as inventory):

```csharp
[DataField, AutoNetworkedField]
[Access(Other = AccessPermissions.ReadWrite)]
public int SecretValue
{
    get => _secretValue;
    set => _secretValue = value;
}
```

### SessionSpecific

For data that differs between clients (for example, the visibility of a masked character).

### NetworkedComponent for Shared only

`[NetworkedComponent]` can be set **only on components in a Shared project**. If you put it on a component in a Client or Server project, it will not work silently - without compilation errors, but also without synchronization.

### IRobustCloneable for reference types

If the network field contains a reference type (class, collection), the type must implement `IRobustCloneable` so that the prediction can correctly save and restore state:

```csharp
[DataField, AutoNetworkedField]
public List<string> Items = new(); // List<T> already implements IRobustCloneable
```

## Debugging network problems

###CVars for simulation

Use in the client or server console:

```
net.fakeloss 0.1       // 10% packet loss
net.fakelagmin 0.1     // Minimum 100ms latency
net.fakelagrand 0.05   // + random 0-50ms
net.fakeduplicates 0.05 // 5% doubles
```

### net.predict

```
net.predict false  // Disable client prediction - you can see the real delay
net.predict true   // Turn back on
```

### Prometheus Metrics

The engine exports metrics:
- `robust_net_sent_packets` / `robust_net_recv_packets`
- `robust_net_sent_bytes` / `robust_net_recv_bytes`
- `robust_net_resent_delay` / `robust_net_resent_hole`
- `robust_net_dropped`

## Connection with other skills

- **SS14 ECS Components** - attribute details `[NetworkedComponent]`, `[AutoGenerateComponentState]`, `[AutoNetworkedField]`
- **SS14 ECS Systems** - patterns for working with network events from systems
- **SS14 ECS Entities** - `EntityUid` vs `NetEntity`, containers and network identification
- **SS14 Prediction** - how the client uses the received states to make predictions

## Synchronization optimization: `DirtyField` instead of the full `Dirty` (addition)

For large network components with multiple `AutoNetworkedField` and field deltas enabled, mark point changes with `DirtyField`.

```csharp
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class ProximityDetectorComponent : Component
{
    [AutoNetworkedField] public TimeSpan NextUpdate = TimeSpan.Zero;
    [AutoNetworkedField] public float Distance = float.PositiveInfinity;
    [AutoNetworkedField] public EntityUid? Target;
}

private void Tick(EntityUid uid, ProximityDetectorComponent comp)
{
    comp.NextUpdate += comp.UpdateCooldown;
    DirtyField(uid, comp, nameof(ProximityDetectorComponent.NextUpdate));
    // The delta will be sent only along the changed field.
}
```

### When to do this

1. One or more specific fields change.
2. The component is “heavy” in terms of the number of network fields.
3. Changes occur frequently.

### Anti-pattern

```csharp
// ❌ Completely dirty for every tick when changing one field:
comp.NextUpdate += comp.UpdateCooldown;
Dirty(uid, comp);
```

Leave the full `Dirty` for cases where a significant part of the state actually changes at the same time.
