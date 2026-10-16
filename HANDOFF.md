# Handoff — The Vig (Unity)

Read this first, every session.

## Where things stand (2026-10-15)

The project was made today from `../the-vig` at 292a1e1 (U1). The sim is ported exactly and proven
against the TS sim (U4); floor 1 is built by code in the new look (U2, U8) and the whole night is
playable by keyboard (U7). PROGRESS.md lists what is and isn't ported. The repo is public at
github.com/ako8177/the-vig-unity. The owner chose to focus on Unity: the TS game is retired for new
work (U11).
Two sessions were started on 2026-10-15: the people (the Collector first) in Blender, and the table
animations.

**Ask the owner** after their first walk round:
1. Does the look land? (`shots/`, or press Play.) The levers: light intensities in `RoomBuilder`
   (coves, downlights, pendants, chandeliers), `Atmosphere` (exposure, bloom, fog), the neon factor in
   `Look.Neon` and `Neon.Text`.
2. Which comes next: the people (the Collector in his chair first), the table animations, the house,
   or sound?

## Next (in order, unless the owner picks)

1. **The people are in** (U13): the Collector and the crowd of 52 (PROGRESS.md). Left: the pit boss
   (he drags a dead Collector out through the staff door in the TS game, `duel.ts` `funeral`), the
   family at the house (`Home.Family`, once the house exists), the smokers' smoke. New models: copy
   `art/people/build_crowd.py`'s pattern (shapes in Unity's frame, origin at the joint, roles by mesh
   name), run it in Blender through the MCP (if port 9876 is closed, launch Blender with
   `--python-expr "import addon_utils; addon_utils.enable('blender_mcp', default_set=True, persistent=True)"`),
   then pose the parts in `People.cs`. **Ask the owner** whether the Collector's look lands, and about
   U13's derivative flag (the seated duel, TS #28/#30/#80).
2. **The table animations**: a 3D die on Hi-Lo's baize and two on Dead Man's, Red Line's card flip, a
   real Wheel of Marks (25 slots from `WheelOfMarks.Layout`), the Bandit's reels rolling, Twenty-One
   dealt card by card (`Feel` holds the timings; the sim already holds results back for them).
3. **Sound**: port the TS synth's effects and the lounge songs (`../the-vig/src/app/sounds.ts`,
   `src/engine/audio/`), or replace them.
4. **The house**, then the title, settings, pause and results screens, and saving meta.

## How to run

- Open the project in Unity Hub (6000.6.5f1), open `Assets/Scenes/Floor1.unity`, press Play. Click to
  capture the mouse, Esc to free it.
- Sim checks: `cd tools/SimCheck && dotnet run -c Release` (about 10 s).
- Shots: `powershell -File tools/unity.ps1 shots` (close the editor first: batch mode can't share the
  project), then look at `shots/*.png`.
- Build: `powershell -File tools/unity.ps1 build` → `Builds/Windows/TheVig.exe`. Self-test:
  `TheVig.exe -vigshot C:\path\out.png -screen-fullscreen 0` saves frame 90 and quits.
- Golden traces: `node tools/golden/build.mjs` regenerates them from `../the-vig` (needs its
  node_modules).

## Gotchas

- `AssetDatabase.ImportPackage` is asynchronous: in batch mode it never finishes. TMP's essential
  resources were unpacked from the .unitypackage by hand and are committed (`Assets/TextMesh Pro`).
- The TMP font (Liberation Sans) lacks the dice faces and the lightning bolt; `TableScreen.Glyph`
  checks the font and falls back. Suits and ★ draw.
- Runtime materials must get shaders from `Resources/VigShaders` (U9) or a build strips their variants.
- The namespace is `Vig.Client`, not `Vig.Game`: `Vig.Game` collides with the sim's `Game` class.
- `Collider` is ambiguous with UnityEngine's: RoomBuilder aliases the sim's.
