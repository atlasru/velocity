# Source-inspired movement

MovementPhysics owns integration/collision response; no CharacterController, Rigidbody or PhysX decides gameplay movement. Solo, server, prediction and tests share the same C# files.

## Tick and parameters

Simulation is **120 Hz** (dt = 1/120 s). Rendering interpolates fixed states. Feet positions use metres and velocities metres/second. The HUD/tuning sliders convert **1 Source unit = 0.0254 m**.

| Parameter | Surf / combat | Bhop | Meaning |
| --- | --- | --- | --- |
| sv_gravity | 20.32 m/s² (800 u/s²) | same | Gravity |
| sv_accelerate | 10 | 10 | Ground acceleration coefficient |
| sv_airaccelerate | 150 | 100 | Air acceleration coefficient |
| sv_friction | 4 | 4 | Ground friction coefficient |
| sv_maxspeed | 8.128 m/s (320 u/s) | same | Input wish speed, not momentum cap |
| sv_stopspeed | 2.54 m/s (100 u/s) | same | Minimum friction control speed |
| sv_jump_impulse | 6.8072 m/s (268 u/s) | same | Vertical jump impulse |
| sv_autobunnyhopping | true | true | Permission to repeat held auto-jump |
| sv_air_max_wishspeed | 0.762 m/s (30 u/s) | same | Air projection cap |
| SpeedLimit | 0 | 0 | Optional total horizontal cap; zero disables |

Settings → Movement tuning edits rules for the next solo/host session. The host transmits validated rules during joining; client preferences cannot override them. Modified rules are practice. Auto-bhop requires both host permission and the player's preference.

Hull: AABB half-width 0.3 m, height 1.8 m standing/1.05 m crouched. Step height 0.3 m. Collision skin 0.001 m. Walkable normal Y is at least 0.7; steeper upward faces surf and receive no ground friction.

## Acceleration and jumping

Yaw defines horizontal forward/right. Joystick/WASD determines wish direction and magnitude. Walk uses 45% wish speed; crouch 34%. Ground friction removes max(horizontal speed, stopspeed) × friction × dt, except on the tick that jumps.

Acceleration adds only along wish direction:

~~~text
remaining = projectionLimit - dot(velocity, wishDirection)
added = min(remaining, accelerate * wishSpeed * dt), if remaining > 0
velocity += wishDirection * added
~~~

Air projection is capped by the smaller of wish speed and sv_air_max_wishspeed. The acceleration term uses uncapped wish speed, matching classic Source community movement behavior. It does not normalize total momentum or give jumps a speed multiplier.

Jump replaces vertical velocity once while retaining horizontal velocity. Auto-bhop repeats on walkable landings while held; manual jump needs release/repress. Gravity is split into two half-steps around swept movement. A step is accepted only if it lands on a walkable face and travels farther than the blocked route.

## Collision and ramps

CollisionWorld expands convex brush planes by AABB support and sweeps the entire intended displacement. Axis/edge bevels handle hull contact at boundaries. Brush bounds prune irrelevant geometry. Render meshes use the same original vertices/triangles as collision.

Up to eight sweeps retain remaining tick time after impacts. Plane clipping removes only velocity entering the plane. Two-plane intersections retain velocity along the crease. Incompatible planes stop movement. Valid transitions preserve tangential momentum; abrupt turns can physically lose the normal component. No speed restoration adds energy.

Gravity projected through steep contacts produces downhill surf speed. Standing is blocked while the taller hull overlaps a ceiling. Course players do not collide with each other. Hitscan uses the same world independently of movement.

Sync is the percentage of airborne ticks with meaningful movement input that gain horizontal speed through air acceleration. It is a training metric, not every community server's exact formula. Jumps, peak speed and tick timers reset on a new run. Checkpoint teleports are practice.

## Scope and verification

The collider supports original static convex brush maps, including triangulated curved banks. It does not implement water, ladders, moving platforms or arbitrary imported concave geometry. New geometry needs real sweep/transition tests.

The harness verifies acceleration, friction, projection, manual/automatic jumping, high-speed sweeps, seams, slope changes, ramp edges, steps, headroom and full course routes via ordinary inputs. Simulated 30/60/120/240 render schedules feed the same tick input; they do not measure GPUs or establish identical ARM/x64 floating-point bits.

Principle references: Valve's [Source SDK movement](https://github.com/ValveSoftware/source-sdk-2013/blob/master/src/game/shared/gamemovement.cpp) and id Software's [Quake III movement](https://github.com/id-Software/Quake-III-Arena/blob/master/code/game/bg_pmove.c). Velocity's implementation and geometry are original; their source/assets are not included.
