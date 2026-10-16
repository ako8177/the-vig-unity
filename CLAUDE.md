# CLAUDE.md — The Vig (Unity)

You are building **The Vig** in Unity: a first-person co-op casino roguelike, ported from the
TypeScript/Three.js game at `../the-vig` (github.com/ako8177/the-vig) for cleaner graphics. Read
`docs/PLAN.md` (what and why), `docs/DESIGN.md` (every number and its derivation), `docs/STYLE.md`
(the art bible, as re-read for a lit game by DECISIONS U2), `PROGRESS.md` (what exists) and
`HANDOFF.md` (where the last session stopped) at the start of every session. `DECISIONS.md` holds the
Unity project's decisions (U1, U2, ...); `docs/DECISIONS-TS.md` is the TS game's log, frozen, which
the data's derivation strings cite. Add to DECISIONS.md, never silently contradict either.

## Stack (decided 2026-10-15, U1-U3)

Unity 6 (6000.6.5f1) · URP (Forward+, HDR, MSAA 4x, SSAO) at full resolution · C# 9 (the sim builds
in Unity and in `tools/SimCheck`, net9.0 held to C# 9) · Input System · TextMeshPro · Windows first ·
everything in the room built by code from the layout data (no hand-placed scene content).

## Architecture rules

- **`Assets/Vig/Sim/` is pure and command-driven** (asmdef `Vig.Sim`, `noEngineReferences: true`).
  It never sees UnityEngine, input, rendering, time or the network. Commands tagged with a player id
  and a tick go in; events come out; every random number comes from the seeded `Rng` inside it. Same
  seed + same command log = same night. One system per class in fixed order in `Game.Systems`:
  clock → movement → tables → marks → lives → cage → home → collector → economy → stats.
- **The port is exact.** `tools/golden/traces` hold the TS sim's state hash and events after every
  tick of 16 driven runs; `tools/SimCheck` replays them through the C# sim and must match every tick.
  JS number semantics live in `Core/Js.cs` (`Js.Round`, `Js.Num`); tables are iterated in
  `State.TableIds` order. A deliberate rule change: change both games and regenerate the traces
  (`node tools/golden/build.mjs`), or record in DECISIONS.md that the TS game is retired (U4).
- Content is typed data in `Assets/Vig/Sim/Data/`. **Every number has a derivation** naming a
  `DESIGN.md` section; SimCheck's data checks enforce it, recompute every table's RTP from its
  outcomes, and check the tier rule.
- **`Assets/Vig/Client/`** (asmdef `Vig.Client`) is presentation: `RoomBuilder` builds the floor from
  `Layout.Floor1`, `Look`/`Textures` make every material and texture, `Neon` the signs, `Screens` the
  table panels, `Hud`, `Play` (the loop: input → commands, events → camera, lights, HUD). It reads sim
  state and sends commands; it never writes state. Sim (x, y, z) is Unity (x, y, -z) (U6).
- `Assets/Vig/Editor/VigTools.cs`: project setup, the screenshot sweep, the Windows build.

## Definition of done (every change)

1. `cd tools/SimCheck && dotnet run -c Release`: every golden trace, replay and data check green.
2. Unity compiles clean: `powershell -File tools/unity.ps1 shots` (it compiles, then renders
   `shots/*.png`).
3. For anything the player sees: the shots taken and **looked at** (Read the PNG). For build-affecting
   changes (shaders, materials, packages): `tools/unity.ps1 build`, then run
   `Builds/Windows/TheVig.exe -vigshot shots/player_selftest.png -screen-fullscreen 0` and look at it.
4. Anything that changes a number: its derivation updated in `docs/DESIGN.md`.
5. `PROGRESS.md` and `HANDOFF.md` updated; `DECISIONS.md` if a decision was made; one commit saying
   what and why.

## Standing instructions (carried over from the TS game)

- **Odds are always printed.** Every table's screen shows tier, win chance, payout and loss cost
  before a bet; only the Blackout team mark hides them, and it says so.
- **No real-money anything**, ever, in any form. Don't propose it.
- **Every table has a tier color. Every risk is shown.** The tier colors (`Look.Low/Mid/High/Coll`)
  never go on anything else.
- **Flag anything derivative.** Buckshot Roulette, CloverPit and Gamble With Your Friends are
  references for one quality each; if something resembles a shipped game, say so in chat and
  DECISIONS.md, and re-derive it or drop it.
- Ask before: changing the tier rule (RTP up, loss cost up), the currency list, the night length, the
  quota curve, or the milestone order. Everything else: decide, write it down, move on.

## Environment

Windows. Unity Hub with 6000.6.5f1 (Windows + WebGL modules). .NET 9 SDK. Node 22 (only for
regenerating the golden traces from `../the-vig`). Blender 5.2 (Steam) with the Blender MCP for
models. Unity's batch mode must not have the project open in the editor at the same time.
