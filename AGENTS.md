# AGENTS.md

Notes for anyone, human or AI agent, changing this code. `README.md` covers playing the game, its controls and
the downloads; this file covers how the code is put together and the rules that keep it working.

## What this is

A garden shooter in the style of Atari's 1980 arcade game Centipede, in C# on .NET 10 and Avalonia 12. One
self-contained executable per platform (Windows, macOS and Linux, x64 and ARM). The pixel art is this game's
own, drawn in code (`Models/Sprites.cs`), and the sounds and tunes are synthesised by `tools/make_sounds.py`.

## Commands

```bash
dotnet build AVACentipede.slnx                    # the SDK version is in global.json (10.0.400 or later)
dotnet test AVACentipede.slnx                     # every test runs headless: no window opens
dotnet run --project src/AVACentipede             # play it; --no-audio for silence
CENTIPEDE_SHOTS=$PWD/Images dotnet test tests/AVACentipede.Tests --filter ScreenshotTests   # refresh the README's pictures
python3 tools/make_sounds.py                      # rebuild src/AVACentipede/Sounds (standard-library Python only)
```

`Directory.Build.props` sets `net10.0`, `TreatWarningsAsErrors`, nullable reference types and implicit usings
for every project: a warning fails the build, so keep it at zero.

## Layout

- `src/AVACentipede/Engine/`: the game, with no Avalonia drawing in it.
  - `GameEngine.cs` runs the states (title, attract, play, initials, high scores) and the fixed-step loop.
  - `GameEngine.Play.cs` steps a wave: the centipede, spider, flea, scorpion, shots and mushrooms.
  - `Entities.cs` has the segments, enemies and the `Controls` record.
  - `AttractPilot.cs` is the computer player, used by the attract mode and the tests.
  - `InputHandler.cs` (keys and mouse) and `ControllerManager.cs` (SDL2 gamepads) produce `Controls`.
  - `HighScoreTable.cs`, `InitialsEntry.cs` and `SoundManager.cs` do what their names say.
- `src/AVACentipede/Models/`: `Field.cs` (the 30x32 grid of 8-pixel cells and the step length) and
  `Sprites.cs` (the pixel art).
- `src/AVACentipede/Views/`: `GameCanvas` (split over partial files) draws the frame and runs the 16 ms timer.
- `src/AVACentipede/Audio/`: the OpenAL Soft audio engine, shared with the author's other AVA games.
- `src/AVACentipede/Assets/Fonts/`: the VT323 font, under the OFL.
- `tests/AVACentipede.Tests/`: xUnit v3 with `Avalonia.Headless.XUnit`. `GameplayTests.cs` plays the engine;
  `ScreenshotTests.cs` draws real frames in a headless window.

## Rules that keep it working

- **Fixed steps.** `GameEngine` collects frame time in an accumulator and advances in fixed 1/60 s steps
  (`Field.Step`), so tests can play deterministically from a seed. Draw random numbers from the engine's
  seeded `_rng` (`new Random(seed)`), never from `Random.Shared`.
- **Engine first, drawing second.** Gameplay belongs in `Engine/`, where tests can reach it without a window.
  `Views/` only reads the game and draws it.
- **Input goes through `InputHandler`.** `Controls.Dy` is negative for up (y grows downwards on the field).
  Keys, D-pad and stick must all keep that sign; the mouse steers by `Aim` instead.
- **SDL under one lock.** `ControllerManager` starts, polls and stops SDL only inside `lock (Sdl)`. The tests
  create many handlers in parallel, and unlocked SDL calls corrupt its heap.
- **Audio.** OpenAL Soft through Silk.NET. `Directory.Build.targets` swaps in the official Windows `soft_oal.dll`
  from `Audio/native/`, which needs no Visual C++ runtime. In a virtual machine `OpenAlConfig` writes a larger
  mixing period to `ALSOFT_CONF` so the sound doesn't break up; a player's own `ALSOFT_CONF` is left alone.
- **Tests never touch the player's files.** High scores live in the app-data folder (`AVACentipede`); tests
  load the table from a fresh folder under the system temp path and use a silent `SoundManager`.

## Making a change

- Add or update tests with every gameplay change. `dotnet test` must pass, with no warnings.
- If the change shows on screen, refresh the screenshots with the command above, and look at them.
- Update `README.md` (features, controls) to match.
- New sounds go into `tools/make_sounds.py`, not as hand-edited WAV files.

## CI and releases

`.github/workflows/build.yml` builds and tests every push and pull request. It then publishes self-contained
single-file builds for win-x64/x86/arm64, linux-x64/arm64/arm and osx-x64/arm64. Every run keeps the builds as
workflow artifacts. Only pushes to `main` replace the assets of the `latest` pre-release, and a `v*` tag makes a
proper release. A release also asks the portfolio site to rebuild.
