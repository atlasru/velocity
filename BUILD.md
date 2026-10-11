# Builds and activation

Pinned Editor: **Unity 6.3 LTS 6000.3.26f1**, URP **17.3.0**. Windows: x64 Mono, .NET Standard, Direct3D 11. Android: IL2CPP ARM64 only, API 29 minimum/API 36 target, GLES 3, landscape. Exported games need no Unity account or activation.

Editor/direct package versions, .NET test SDK and GitHub action revisions are pinned. VelocityBuild.Configure generates URP assets and applies platform settings on import and before every export. Unity must resolve its package graph during the first licensed import; commit the resulting Packages/packages-lock.json after successful import. An Editor-generated lockfile is not fabricated here. Byte-identical binaries are not promised; signing and toolchain metadata affect hashes.

## Observed blocker

Actual Windows and Android GameCI attempts both failed before compilation:

> Missing Unity License File and no Serial was found.

No activated license or serial was available in Actions secrets. A failed activation is not a successful compile/export. Independent source checks and source/test artifacts can still run.

Add secrets under [repository Actions secrets](https://github.com/atlasru/velocity/settings/secrets/actions):

| License route | Required secrets |
| --- | --- |
| Personal with valid activated .ulf | UNITY_LICENSE (complete .ulf contents), UNITY_EMAIL, UNITY_PASSWORD |
| Pro serial activation | UNITY_SERIAL, UNITY_EMAIL, UNITY_PASSWORD |

Acquire/activate a valid license using [GameCI activation guidance](https://game.ci/docs/github/activation/). Unity 6 Personal manual activation can differ from older versions: credentials alone do not provide a missing license file. If Hub/account licensing cannot issue a usable .ulf, use a supported activation route. Never paste credentials in chat or commit license files, passwords or serials.

After activation is configured, rerun failed jobs or manually dispatch **Velocity CI** on feature/velocity-unity. Inspect actual Editor and build logs; any subsequent package, API, shader or toolchain failures must be fixed before reporting successful binaries. Unity 6 LTS remains the only engine.

## CI

.github/workflows/ci.yml runs on main, feature branches, PRs, manual dispatch and v* tags:

1. Validate the workflow and packaging scripts, check Unity C# syntax, compile the shared core against .NET Standard 2.1, run real movement/course/UDP tests.
2. Upload an exact source ZIP as Velocity-source even if another check fails.
3. GameCI attempts EditMode and PlayMode tests with the pinned Editor. The classic v4.3.2 Docker runner is pinned to avoid the observed v4.4.0 CLI argument failure. No code-coverage package is added to the committed project.
4. GameCI attempts both real exports sequentially. Export attempts still run if activation blocks Editor testing and core checks passed.
5. Packaging requires an actual x64 PE plus Unity runtime/data/core assemblies, or an ARM64-only IL2CPP APK with correct ELF architecture and a signature verified by SDK apksigner.
6. Windows is zipped with the complete output directory. Metadata records commit/editor/target/version/SHA-256. Development artifacts last 30 days; source lasts 90.
7. A tag publishes only after core tests, Unity tests and both exports pass. It creates a draft, uploads Windows ZIP, Android APK, source, metadata and checksums, then publishes.

Missing activation fails visibly. No fake executable/APK is created. Export architecture/signature checks do not verify launch/install on hardware.

## Android signing

Branch/manual builds can use a debug keystore. Different CI workers can produce different debug certificates; updating an installed development build may require uninstalling it first.

Tagged releases require these additional secrets for stable signing:

| Secret | Content |
| --- | --- |
| ANDROID_KEYSTORE_BASE64 | Base64 bytes of your private keystore |
| ANDROID_KEYSTORE_PASSWORD | Keystore password |
| ANDROID_KEY_ALIAS | Signing alias |
| ANDROID_KEY_PASSWORD | Alias password |

GameCI decodes the keystore in the build environment. The custom method consumes its signing arguments and clears password fields after export. Keys/license files are ignored by Git. A tagged Android export fails early if signing secrets are absent.

Push a **vMAJOR.MINOR.PATCH** tag on the intended tested commit. Its version becomes the application version; Android version code is 100 + GITHUB_RUN_NUMBER. Published releases are not overwritten by this workflow; changes need a new tag.

## Local export

Install the pinned Editor plus Windows Build Support (Mono), Android Build Support, SDK/NDK/OpenJDK through Hub. Activate your license, import the project, run Velocity → Configure project.

Replace Unity below with your Editor executable path:

~~~sh
Unity -batchmode -quit -projectPath . -buildTarget StandaloneWindows64 -executeMethod Velocity.Editor.VelocityBuild.Build -buildPath build/StandaloneWindows64/Velocity.exe -logFile build/windows.log
Unity -batchmode -quit -projectPath . -buildTarget Android -executeMethod Velocity.Editor.VelocityBuild.Build -buildPath build/Android/Velocity.apk -logFile build/android.log
~~~

Windows Mono can be cross-exported by the Linux GameCI image. Android IL2CPP needs the Editor's supported SDK/NDK. INTERNET permission permits LAN sockets; there is no online service.

After a successful export run python3 Tools/package_build.py StandaloneWindows64 or python3 Tools/package_build.py Android. Android also needs SDK apksigner under ANDROID_HOME/ANDROID_SDK_ROOT. Packaging intentionally fails if real exports are absent.

Use Unity Test Runner for EditMode/PlayMode tests. Follow [QA](Docs/QA.md) for Windows launch, APK install/launch, device touch/performance checks and LAN in both hosting directions.
