# Velocity

Offline first-person surf, bunnyhop and combat movement game. Windows x64 and Android ARM64.

**Engine: Unity 6.3 LTS, C#, Universal Render Pipeline.** Godot and Unity services are not used.

Development branch: `feature/velocity-unity`. Unity Editor is required to run the game and produce binaries. The core simulation also compiles as an independent .NET test harness.

## Development

Open the repository in Unity **6000.3.26f1** with Windows Mono and Android IL2CPP modules installed. Run **Velocity → Configure project**, open `Assets/Velocity/Scenes/Bootstrap.unity`, press Play.

```sh
dotnet run --project Tests/CoreHarness -c Release
```

GameCI builds and licensing instructions are in [BUILD.md](BUILD.md). Build status must be checked in Actions; source commits are not evidence of successful Windows or Android builds.
