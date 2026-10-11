# QA evidence and acceptance

## Verified automated behavior

The production engine-independent core has been compiled against .NET Standard 2.1 with warnings as errors. Local C# syntax parsing reported 22 Unity/core/test files with zero syntax errors This is not Unity API compilation. The latest expanded harness contains **43** real tests; JSON results in Actions are the authoritative result for each commit.

Local runs passed 43/43, including course completion from spawn through ordinary movement inputs (no teleports), continuous ramp/edge sweeps, frame-schedule invariance, actual UDP sockets and filesystem replacement. CI phase one passed 15/15; phase two passed 33/33:

- [Phase one actual run](https://github.com/atlasru/velocity/actions/runs/38169995113)
- [Phase two actual run](https://github.com/atlasru/velocity/actions/runs/38172540632)

The final expanded suite and packaging workflow are checked by the next branch push. Inspect its actual result under [Velocity CI](https://github.com/atlasru/velocity/actions). Do not infer a successful check/export merely from a source commit.

| Area | Automated coverage |
| --- | --- |
| Ground | Acceleration, wish-speed cap, friction, steps, standing clearance |
| Air / surf | Projection cap without normalization, preserved jump momentum, ramp gravity gain, coplanar and triangulated seams, slope change, edges, high-speed continuous collision |
| Bhop | Manual release/repress and held automatic chains, complete Bhop Flow route |
| Courses / timing | Complete Surf Beginner route, ordered CPs, finish rejection, tick precision, practice teleports, modified rules stay practice |
| Frame scheduling | Identical fixed-input state at simulated 30, 60, 120 and 240 rendering Hz |
| Touch core | Simultaneous joystick/look/jump ownership, independent release, assist changes input only |
| Combat | Rifle cadence, semi-auto edge, reload/ammo/cooldown, repeatable recoil, world occlusion, teams, death/respawn, bounded history rewind |
| Protocol | State roundtrip/MTU, truncated/oversized/nonfinite input rejection, bounded sequence queue, replay and interpolation |
| Actual UDP | Manual loopback join, dropped packets, authoritative movement, disconnect/reconnect, host restart/new token, eight-player capacity/ninth rejection, server-resolved remote combat |
| Discovery | Actual directed UDP query/reply |
| Filesystem | Flushed replacement, interrupted temporary write, corrupt-primary backup recovery |

UDP tests run both ends on Linux. Names or host-role labels do not constitute Windows/Android testing. Broadcast on a real Wi-Fi network is separate from the directed query test.

## Unity and exports: blocked

Both Windows and Android GameCI jobs have actually run and failed before compilation with Missing Unity License File and no Serial was found. UNITY_LICENSE/UNITY_SERIAL and account secrets were not configured. BUILD.md lists exact secrets.

Committed Unity tests cover collision mesh coherence, starting all three maps with mesh/camera/clear hull and URP, solo pause/resume, settings/records JSON restart and backup recovery. They have **not run successfully**; the Editor-test CI job requires activation.

No Windows executable has launched, no APK has installed/launched, no physical multitouch or device frame-rate measurement has occurred. There is no successful playable release to certify.

## Required platform acceptance after activation

Record device model, OS, CPU/GPU, commit, APK certificate/version, render settings and pass/fail evidence.

1. Import/build with 6000.3.26f1. Pass Unity EditMode/PlayMode tests, inspect compiler and shader logs, verify packaged architectures/signature.
2. Windows 10/11 x64: extract ZIP on a clean machine, launch, complete both courses, play solo arena, change/rebind settings, exit/relaunch and check settings/PB.
3. Android 10–16 ARM64: install/launch, test landscape safe areas, four concurrent touches (move/look/jump/fire), release/cancel events, pause/focus loss, layout sizes/positions/transparency and persistence after relaunch.
4. Windows host → Android client: discovery and manual IP, names/list, both course completions, fast surf correction, FFA/team combat, kill feed/scoreboard, respawn, disconnect and reconnect.
5. Android host → Windows client: repeat step 4. Also test an isolated Wi-Fi network/hotspot without internet and manual fallback when broadcast is blocked.
6. Fill an eight-player LAN session; reject a ninth, remove/rejoin players, exercise packet delay/loss and host shutdown. Check responsiveness and bounded correction during fast movement.
7. Profile Windows at 1080p low and Android at target settings. Measure CPU/GPU frame times for courses/arena and eight players. Check 60 FPS target and weak-device 30 FPS fallback. Do not replace measurements with Application.targetFrameRate.
8. Compare recorded movement input at 30/60/120/240 FPS on Windows and applicable Android caps. Check manual jump taps, ramp transitions, edges, high speed and timers.
9. Test APK upgrade with the stable signing key and local storage recovery on Android. Perform a final offline launch.

A release is complete only when these platform checks establish the user's definition of done. Until then KNOWN_LIMITATIONS.md remains part of delivery.
