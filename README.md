# Velocity

Offline first-person **surf, bunnyhop and LAN combat**, built with **Unity 6.3 LTS 6000.3.26f1, C# and URP 17.3.0**. Targets Windows 10/11 x64 and Android 10–16 ARM64. Original procedural geometry, weapons and audio; no player account, telemetry, ads or online game service.

**Delivery status:** movement, courses, input, LAN simulation, combat, UI and persistence are implemented in source. The engine-independent suite has 43 tests. Actual GameCI export attempts have failed at Unity activation because repository license secrets are missing. No Windows executable or Android APK has been verified or published. Unity compilation, rendering and physical device validation remain pending; see [QA](Docs/QA.md) and [known limitations](KNOWN_LIMITATIONS.md).

## Exported builds

Successful development builds appear under [Actions](https://github.com/atlasru/velocity/actions). The configured outputs are:

| Artifact | Contents |
| --- | --- |
| Velocity-source | ZIP of the exact committed Unity project, independent of activation |
| velocity-core-test-results | Actual automated test results JSON |
| Velocity-StandaloneWindows64 | Portable Velocity-Windows-x64.zip and build metadata |
| Velocity-Android | Installable Velocity-Android-arm64.apk and build metadata |

Windows: extract the entire ZIP and launch **Velocity.exe**, keeping UnityPlayer.dll, Velocity_Data and all other exported folders together.

Android: copy the APK to the phone and install it, allowing installation from the chosen file manager when Android asks. It requires an ARM64 device, Android 10+, and local-network access. Internet connectivity is unnecessary. A changed development signing certificate can require uninstalling the older APK.

Successful tagged workflows also publish at [Releases](https://github.com/atlasru/velocity/releases). The binary names above describe configured outputs, not already available releases.

## Courses and combat

- **Surf Beginner:** three wide ramps with alternating banks, two checkpoints, a finish platform and an optional curved bank route. Drop onto a bank, hold A/D toward the surface, and turn to maintain the line. Gravity and air strafing produce speed; jumping never awards a boost.
- **Bhop Flow:** 24 consecutive platforms, a forgiving side route, an elevated shortcut, four checkpoints and a finish zone. Hold jump for auto-bhop or disable it and time each landing. Rotate the camera while air strafing.
- **Combat Arena:** open surf paths, raised platforms, spawns and ammunition pickups. LAN supports FFA or alternating teams. Solo has three stationary, damageable practice targets with the same health and respawn rules.

The timer starts when you leave the start zone. Checkpoints must be reached in order before finishing. Restart begins a clean run. Falling or teleporting to a checkpoint marks **PRACTICE**; practice and modified-physics runs cannot replace personal bests. Course players do not collide with one another. Movement and timing use a fixed 120 Hz simulation.

## Desktop controls

| Action | Default |
| --- | --- |
| Move / strafe | WASD |
| Look | Mouse |
| Jump / held automatic bhop | Space |
| Crouch / walk | Ctrl / Shift |
| Fire / aim | Left / right mouse |
| Reload in combat | R |
| Rifle / sniper / pistol / knife | 1 / 2 / 3 / 4 |
| Restart course | R or F5 |
| Checkpoint teleport (practice) | T |
| Scoreboard | Hold Tab |
| Pause / menu | Escape |

Settings allow movement/combat/scoreboard rebinding, un-smoothed relative mouse sensitivity, FOV, hold/toggle crouch, auto-bhop, crosshair, training HUD, graphics and frame limit. Solo pauses; LAN simulation continues while a menu is open.

## Android controls

The left thumb drives a floating joystick; swipe on the right to look. Jump/crouch remain available in movement courses. Combat adds fire, aim, reload and weapon cycle. Separate touch IDs keep simultaneous joystick, camera and button ownership. HUD buttons provide menu, scoreboard, restart and checkpoint actions.

**Classic** keeps full joystick movement. **Surf Assist** widens usable lateral thumb input and suppresses small accidental forward input during a strafe. It never turns the camera, changes velocity or aims for the player.

Settings expose touch sensitivity, opacity, overall and individual button size. During a session choose Settings → Drag touch buttons, position the controls, then Finish. Layout is saved and scaled to the landscape safe area.

## LAN

1. Connect devices to the same Wi-Fi/Ethernet network.
2. On either choose Host LAN, a map and, for combat, FFA or teams. Capacity is eight including the host.
3. Choose Join LAN on the other device and select the discovered server. If broadcast is blocked, enter the host IPv4 address, such as 192.168.1.100.
4. The host pause menu lists local IPs. Allow UDP **27027** (gameplay) and **27028** (discovery) through the host firewall. Guest Wi-Fi / AP client isolation can prevent local connections.

The shared C# protocol sends sequenced inputs rather than trusting client transforms. The host validates input and owns simulation/damage. Clients predict at 120 Hz, reconcile with 30 Hz authoritative snapshots and interpolate other players. Reconnect through the saved IP after disconnect. Real loopback UDP has been exercised on Linux; Windows↔Android and physical Wi-Fi discovery remain unverified.

## Develop

Check out **feature/velocity-unity** until merged. Install the pinned Unity Editor with Windows Mono and Android IL2CPP support (SDK/NDK/OpenJDK). Open the project, run **Velocity → Configure project**, open Assets/Velocity/Scenes/Bootstrap.unity, press Play. First import generates the configured URP assets and player settings.

Tests without Unity activation:

~~~sh
dotnet run --project Tests/CoreHarness -c Release
dotnet build Tests/Compatibility -c Release
dotnet run --project Tools/SyntaxCheck -c Release
~~~

[BUILD.md](BUILD.md) covers activation, signing, exports and releases. [Physics](Docs/PHYSICS.md) covers parameters and collision behavior. [Architecture](Docs/ARCHITECTURE.md) covers interfaces/ownership. [QA](Docs/QA.md) separates automated results from pending device acceptance.

Rendering uses batched map meshes, a single surface-line batch, small URP shaders and no shadows/post-processing. Android adapts internal resolution down to 50% and can fall back to 30 FPS. Stable 60 FPS on mid-range Android and low-end integrated Windows GPUs is a target awaiting hardware measurement.
