# AGENTS.md

Notes for anyone, human or AI agent, changing this code. `README.md` covers playing the game, its controls and
what the tests check; this file covers how the code is put together and the rules that keep it working.

## What this is

A colour vector game in the style of Atari and General Computer's 1982 arcade game Quantum, in C# on .NET 10 and
Avalonia 12. One self-contained executable per platform (Windows, macOS and Linux, x64 and ARM). The vector
graphics are drawn in code, and the sounds are synthesised by `tools/make_sounds.py`.

## Commands

```bash
dotnet build AVAQuantum.slnx                 # the SDK version is in global.json (10.0.400 or later)
dotnet test AVAQuantum.slnx                  # every test runs headless: no window opens
dotnet run --project src/AVAQuantum          # play it; --no-audio for silence
QUANTUM_SHOTS=$PWD/Images dotnet test tests/AVAQuantum.Tests --filter ScreenshotTests   # refresh the README's pictures
QUANTUM_PROBE=1 dotnet test tests/AVAQuantum.Tests --filter PilotProbe   # how the computer player gets on, for tuning
python3 tools/make_sounds.py                 # rebuild src/AVAQuantum/Sounds (standard-library Python only)
```

`QUANTUM_PROBE` can also be a wave number to start there, and `QUANTUM_SEEDS` (default `1,2,3`) picks the games.
Without `QUANTUM_PROBE` the probe test does nothing.

`Directory.Build.props` sets `TreatWarningsAsErrors`, nullable reference types and implicit usings for every
project: a warning fails the build, so keep it at zero.

## Layout

- `src/AVAQuantum/Engine/`: the game, with no Avalonia drawing in it.
  - `GameEngine.cs` runs the states (title, demonstration, play, high scores) and the fixed-step loop.
  - `World.cs` is one game's arena: the atom and its electrons, the particles, and the probe with its trail.
    `World.Step` moves it all on a sixtieth of a second.
  - `Geometry.cs` has the sums behind catching: where lines cross, a loop's area, and what is inside it.
  - `WaveBook.cs` says what each wave holds and how hard it is.
  - `Entities.cs` has the particle kinds, the probe, catches, sparks and `Palette`.
  - `Field.cs` has `Controls`, `Pt` and the screen measures (`Field`).
  - `Pilot.cs` is the computer player, used by the demonstration and the tests.
  - `InputHandler.cs` (keys and mouse) and `ControllerManager.cs` (SDL2 gamepads) produce `Controls`.
  - `HighScoreTable.cs`, `InitialsEntry.cs` and `SoundManager.cs` do what their names say.
- `src/AVAQuantum/Views/`: `GameCanvas` (split over two partial files) draws the fixed 1024x768 screen and runs the
  60 Hz timer.
- `src/AVAQuantum/Audio/`: the OpenAL Soft audio engine, shared with the author's other AVA games.
- `tests/AVAQuantum.Tests/`: xUnit v3 with `Avalonia.Headless.XUnit`. `Harness.cs` sets up worlds and games.
  There are gameplay, input, shift-release and screenshot tests, and the pilot probe.

## Rules that keep it working

- **Fixed steps.** `GameEngine.Update` advances the world in fixed 1/60 s steps (`Field.Step`), independent of the
  frame rate, so tests play deterministically from a seed. Draw random numbers from the world's seeded `Rng`
  (`w.Rng`, set by `World.Start(seed)`), never from `Random.Shared`.
- **Engine first, drawing second.** Gameplay belongs in `Engine/`, where tests can reach it without a window.
  `Views/` only reads the world and draws it.
- **Input goes through `InputHandler`.** `Controls` is a push each way (-1 to 1), and down the screen is +1 (y grows
  down the screen), on keys and stick alike. With `Steer` set, it is instead a point for the probe to race to (the
  mouse and the computer player). Test new controls through `InputHandler`, as `InputTests` does, not by setting
  `Controls` directly.
  - Keys are tracked by `PhysicalKey`, because on some Linux setups Avalonia reports the release of Shift as
    CapsLock. `ShiftReleaseTests` covers this.
- **SDL under one lock.** `ControllerManager` starts, polls and stops SDL only inside `lock (Sdl)`. The tests
  create many handlers in parallel, and unlocked SDL calls corrupt its heap.
- **Audio.** OpenAL Soft through Silk.NET. `Directory.Build.targets` swaps in the official Windows `soft_oal.dll`
  from `Audio/native/`, which needs no Visual C++ runtime. In a virtual machine `OpenAlConfig` writes a larger
  mixing period to `ALSOFT_CONF` so the sound doesn't break up.
- **Tests never touch the player's files.** High scores live in the app-data folder (`AVAQuantum`); tests load the
  table from a temporary folder (`Harness.TempFolder()`) and run with `Silent = true`.

## Making a change

- Add or update tests with every gameplay change. `dotnet test` must pass, with no warnings.
- If the change shows on screen, refresh the screenshots with the command above, and look at them.
- Update `README.md` (features, controls, the test list) to match.
- New sounds go into `tools/make_sounds.py`, not as hand-edited WAV files.

## CI and releases

`.github/workflows/build.yml` builds and tests every push. It then publishes self-contained single-file builds for
win-x64/x86/arm64, linux-x64/arm64/arm and osx-x64/arm64. Every run keeps the builds as workflow artifacts.
Only pushes to `main` replace the assets of the `latest` pre-release, and a `v*` tag makes a proper release.
