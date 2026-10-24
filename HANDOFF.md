# Handoff — The Vig (Unity)

Read this first, every session.

## Where things stand (2026-10-24)

**Latest**: craps' one-roll bets (U34: FIELD, ANY CRAPS, ELEVEN, each returning 34/36) are committed. They were
written on 2026-10-16 by a session that stopped before committing; on 2026-10-24 they were checked (SimCheck
293 green, shots looked at) and committed. **Ask the owner** whether the side bets earn their button.


The big pass (DECISIONS U15-U25, PROGRESS.md) is in, then the pace bots (U26), pause/settings/results and
chips on the felt (U27), and a fast boot with working bloom in the build (U28). The big pass: clickable tables, real dice, floor 2 with four new
games, the easy quota curve, ten new charms and a nightly catalogue, Let It Ride, the progressive, the
house, a title screen, sound. The two sessions from 2026-10-15 (people, table animations) stopped on a
usage limit before committing; their work was committed as it stood (people: U13, U14).

**Ask the owner** after they play:
1. How the seated bar feels (chips, buttons), and whether Crash and Let It Ride land.
2. The quota curve: keep easy as the default, or move back toward house rules now floor 2 exists?
3. Which of IDEAS.md's "next" list to build first.

## Next (in order, unless the owner picks)

1. **More side bets** where they fit: PAIRS at Twenty-One (U30) and craps' one-roll bets (U34) are in; a
   roulette straight-up already exists. Next candidates: Crash or Peg Drop, only if one earns its place.
2. **Floor 2's people**: dealers at roulette (spin the ball), craps (the stickman), a crowd that watches Crash.
3. **The house's furniture** modelled in Blender (the couch, the TV, the car), the family's own look.
4. A Collector of floor 2's own.
5. Use the charm icons beyond the catalogue: the HUD's charm line, the Boon wheel's charm slot, the post.

## How to run

- Open the project in Unity Hub (6000.6.5f1), open `Assets/Scenes/Floor1.unity`, press Play: the title
  screen. Or run `Builds/Windows/TheVig.exe`.
- Sim checks: `cd tools/SimCheck && dotnet run -c Release` (about 10 s).
- Shots: `powershell -File tools/unity.ps1 shots` (close the editor first: batch mode can't share the
  project), then look at `shots/*.png`.
- Build: `powershell -File tools/unity.ps1 build` → `Builds/Windows/TheVig.exe`. Self-test:
  `TheVig.exe -vigshot C:\path\out.png -screen-fullscreen 0` saves frame 90 and quits.
- Golden traces: `node tools/golden/build.mjs` regenerates them from `../the-vig` (needs its
  node_modules).

## Gotchas

- The build bakes procedural textures to `Assets/Vig/Resources/VigTex` (git-ignored) and the player loads
  those; the editor always draws them fresh (U28). A new texture made through `Textures.Make` is baked
  automatically if any world built during the bake uses it.
- Keep URP's post-processing strip flags off (`UniversalRenderPipelineGlobalSettings.asset`): the volume
  is made at runtime, so URP strips Bloom from builds otherwise.
- `AssetDatabase.ImportPackage` is asynchronous: in batch mode it never finishes. TMP's essential
  resources were unpacked from the .unitypackage by hand and are committed (`Assets/TextMesh Pro`).
- The TMP font (Liberation Sans) lacks the dice faces and the lightning bolt; `TableScreen.Glyph`
  checks the font and falls back. Suits and ★ draw.
- Runtime materials must get shaders from `Resources/VigShaders` (U9) or a build strips their variants.
- The namespace is `Vig.Client`, not `Vig.Game`: `Vig.Game` collides with the sim's `Game` class.
- `Collider` is ambiguous with UnityEngine's: RoomBuilder aliases the sim's.
