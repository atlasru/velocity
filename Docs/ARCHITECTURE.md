# Architecture and interfaces

Unity supplies scene lifecycle, presentation, input, audio and fixed scheduling. The engine-independent Velocity.Core assembly owns movement, static collision, timing, combat and networking. It contains no Unity references. Tests compile those exact production files, not a replacement physics implementation.

| Responsibility | Owner / public entry points |
| --- | --- |
| MovementPhysics | MovementPhysics.Step(ref state, input, world); Config; PlayerHull; collision-plane Clip |
| Surf surfaces | ConvexBrush / BrushFactory.Ramp and TrianglePrism; planes, bounds and shared render vertices |
| DesktopInput | DesktopInput.Read(settings, yaw, pitch, weapon), relative camera input in PlayerInputSource |
| MobileInput | MobileInput.Poll; MobileControls.Update(touches, aspect, combat), separate touch-ID owners |
| PlayerController | BeforeTick and LateUpdate interpolate local camera, correction and remote avatar presentation |
| Weapons | WeaponSystem.Step(state, input, tick) returns a deterministic Shot; catalog defines handling |
| Damage | DamageSystem.Apply(simulation, shooter, input, shot); host-only historical hitscan and world occlusion |
| NetworkManager | LanServer.Pump/Step, LanClient.Pump/Step; real UDP transport, bounded input queues and snapshots |
| LANDiscovery | LanDiscovery.Pump/Query; broadcast/directed UDP search and expiring server entries |
| GameSession | Unity lifecycle, solo/host/join, rule selection, fixed ticks, pause, cleanup and records |
| TimerSystem | Tick-based start/finish and ordered checkpoint progression per player |
| Checkpoints | MapZone spawn definitions; GameSimulation.Restart(player, checkpoint) marks teleports as practice |
| MapManager | MapCatalog collision definitions and Unity MapManager batched rendering from the same vertices |
| SettingsManager | Validated settings/records, persistentDataPath, AtomicFile replacement and backup |
| UIManager | Responsive uGUI menus, HUD, scoreboard, settings/rebinding and touch layout editing |

## Simulation

PlayerInput contains sequence, estimated server tick, bounded forward/side, yaw/pitch, buttons and weapon choice. MovementState contains feet position, velocity, grounded/crouched/jump edge state, angles and training counters. PlayerState also contains authoritative health, scores, timing and complete weapon cooldown/ammo state.

GameSimulation.AdvanceTick increments once; Simulate applies at most one movement input per player per tick. A client sets authoritative=false, which predicts movement, timer and weapon handling without applying damage. Only the host applies DamageSystem. Pause stops solo ticks; LAN keeps neutral input and continues serving peers.

IMovementWorld exposes Sweep and Overlaps. CollisionWorld's convex sweeps are independent of Unity colliders. Map rendering is presentation only; player/world PhysX components do not decide movement. Combat Raycast uses the same brush world. Course players cannot block one another.

## LAN protocol

UDP gameplay port is 27027; discovery is 27028. A versioned little-endian binary protocol uses a 1400-byte maximum. Hello carries a fresh nonce/name; Welcome echoes the nonce and supplies token, ID, map, team rules, tick and movement settings. Ready confirms the join. Inputs contain up to 12 recent commands for loss recovery. Snapshots contain up to eight complete player states and four repeated kill events. Endpoints/tokens bind session traffic. Names and packet fields are bounded/validated.

Clients simulate at 120 Hz and send input bundles at 60 Hz. ServerInputQueue ignores duplicates/stale commands, bounds future sequences and queue size, buffers briefly and consumes one sequence per server tick. Missing input repeats held movement while clearing restart/checkpoint pulses. Snapshot rate is 30 Hz. Peer rate limits, 10-second timeouts and capacity limits bound network work.

PredictionBuffer saves unacknowledged inputs. A newer snapshot replaces local authoritative state, removes acknowledged inputs and replays the remainder with the same physics. Small position error decays in presentation only; teleport-sized corrections snap. RemoteInterpolation renders 100 ms behind the estimated server tick and extrapolates at most three ticks, never blindly setting client-authored transforms. A new server token clears stale prediction/roster/sequences and applies new host rules.

Damage rewinds target hulls within the last 24 server ticks (200 ms), using a 32-tick history. The host validates cadence, ammo, reload, death and team filtering. Restart/checkpoint commands cannot refill combat health or renew protection.

The transport is real System.Net.Sockets UDP, shared by Windows Mono and Android IL2CPP. Linux loopback tests are evidence of this protocol implementation, not evidence that Windows and Android devices connected.

## Content and persistence

MapCatalog creates three static brush maps and start/finish/checkpoint/pickup zones. Adjacent bank triangles share rail vertices. Unity batches each map material and all surface lines; materials use small URP shaders retained explicitly during build. Editor VelocityBuild.Configure creates pipeline settings and the Bootstrap build scene.

Settings use Unity JsonUtility. AtomicFile flushes a temporary file and replaces the primary with a backup. Records reject practice/custom-rule runs. Storage is local, with no account or synchronization service. The core file replacement is tested; Unity serialization and target storage tests await activation/device runs.

Public value types are intentionally small; high-level Unity objects never flow into core simulation. Add a map through brush/zone definitions, a weapon through WeaponCatalog, or an input device through PlayerInputSource. Changes to encoded state require a protocol version change and real encode/reconciliation tests.
