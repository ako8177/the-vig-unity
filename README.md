# The Vig (Unity)

A first-person co-op casino roguelike: you're trapped in an underground casino, and every night the
Collector wants more. Gamble a shared bank up to his quota at tables whose risk you can read on their
neon, then pay him at midnight, or play him for it.

This is the Unity version of [the-vig](https://github.com/ako8177/the-vig) (TypeScript/Three.js),
rebuilt for cleaner graphics: URP at full resolution, real lights and neon, procedural materials.
The game's rules are the same code, ported exactly to C# and checked against the original tick by
tick.

## Play

Open in Unity 6 (6000.6.5f1), open `Assets/Scenes/Floor1.unity`, press Play.

| | |
|---|---|
| WASD, Shift, mouse | walk, run, look (click to capture the mouse, Esc to free it) |
| E | sit at a table, use the computer / mailbox / car at home, call the night at the Collector's table |
| Mouse, seated | chips build your bet; the table's buttons play it; LET IT RIDE after a win |
| `[` `]`, X | the bet down / up, stand up (the table's own keys still work too) |
| 1-4 | order a drink at the bar |

The title screen sets the run: quotas (easy +$200 a night, or house rules x1.5), the floor to start
on, and whether to start at the house.

## Develop

- `Assets/Vig/Sim`: the game's rules (pure C#, no Unity). `tools/SimCheck`: its checks
  (`dotnet run -c Release`). `tools/golden`: traces from the TS game that the C# sim must match.
- `Assets/Vig/Client`: the room, the screens, the HUD, the loop. `Assets/Vig/Editor`: setup,
  screenshots, the Windows build (`tools/unity.ps1 shots|setup|build`).
- `docs/`: the plan, the design (every number's derivation), the games, the art bible.
- `CLAUDE.md`, `DECISIONS.md`, `PROGRESS.md`, `HANDOFF.md`: how the project is run.

No real money, ever, in any form.
