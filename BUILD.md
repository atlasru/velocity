# Reproducible builds

Pinned editor: Unity 6.3 LTS **6000.3.26f1**. URP **17.3.0**. Windows uses Mono; Android uses IL2CPP, ARM64 only, API 29 minimum and API 36 target. No Unity account is needed to play exported binaries.

## GitHub Actions activation

GameCI requires an activated Unity license. For Personal, add repository Actions secrets:

* `UNITY_LICENSE`: complete contents of the Unity Hub-activated `.ulf` file.
* `UNITY_EMAIL`: Unity account email.
* `UNITY_PASSWORD`: Unity account password.

For Pro, use `UNITY_SERIAL`, `UNITY_EMAIL`, `UNITY_PASSWORD` instead of `UNITY_LICENSE`. Do not commit `.ulf`, `.alf`, passwords, signing keys or serial numbers. Unity 6 Personal activation availability depends on Unity Hub/account licensing; use the current [GameCI activation instructions](https://game.ci/docs/github/activation/) if Hub does not issue a usable license file. Credentials must be supplied via repository settings, never chat or source files.

`.github/workflows/ci.yml` runs core tests without Unity activation and then attempts both real GameCI builds. Missing activation fails the build job and does not create a fake executable/APK.

## Local

Install the pinned Editor and Windows Build Support (Mono), Android Build Support including SDK/NDK/OpenJDK through Hub. Activate your own valid Unity license. Open the project; package resolution and generated render assets are automatic. Use **Velocity → Configure project**.

Windows:
```sh
Unity -batchmode -quit -projectPath . -buildTarget StandaloneWindows64 -executeMethod Velocity.Editor.VelocityBuild.Build -buildPath build/StandaloneWindows64/Velocity.exe -logFile build/windows.log
```

Android:
```sh
Unity -batchmode -quit -projectPath . -buildTarget Android -executeMethod Velocity.Editor.VelocityBuild.Build -buildPath build/Android/Velocity.apk -logFile build/android.log
```

Keep the entire Windows output directory together. Zip it for portable distribution. Android development artifacts use Unity's development/debug signing key; a stable private release signing key is required for installations to update across CI runs.
