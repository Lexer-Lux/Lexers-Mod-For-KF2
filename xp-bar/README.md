# XP Bar

Independent source mod for Lexer's Verdite2 fork: Japanese King's Field II,
English disc SLUS-00158. Enable it in the native Mods menu or through Lexeditor's
Mods tab. The folder alone does not enable it in a running game.

A muted gold fill sits on a dark track flush with the bottom of
the game picture. The track reaches both absolute picture edges at 4:3 and wider
aspects. It has no inset, border, text or interactive overlay. It uses ordinary
native POLY_F4 packets in the game's ordering table, behind screen tints.

The native mod gear exposes **Bar height (native pixels)** from 1 to 16, initially
4. Both the track and fill use the same height, extending upward from the absolute
bottom. The value is saved in the port's native preferences. Existing installs
without a saved value receive the four-pixel default.

Progress is `(current XP - this level's starting XP) / (next level's XP - starting XP)`.
Current XP, next XP and level are read from the running game. The previous
threshold comes from the game's loaded level table; levels beyond the table use
the final interval, as the native level-up routine does. The XP or level cap shows
a complete bar. Values outside an interval are clamped without changing game data.

The bar follows native HUD visibility, hides on loading/title/death screens and
world redraws behind native menus or messages, and reads fresh state every draw.
Newly earned progress appears immediately in pale gold. The regular fill eases
over it in 0.45 seconds, restarting from the current fill if another award
arrives. On level-up, the bar completes the current lap and wraps into the next.
Load, New Game and disable reset the animation. The bar only reads progression.

## Verification

Focused checks in `tests/Focused.csproj` call the actual mod draw hook against
blank private emulated RAM. They inspect native packet coordinates at heights
1, 4 and 16 in 4:3, 16:9 and 21:9; track/fill ordering; exact-fit allocation;
both native cursors; overflow/alignment guards; unchanged character progress;
HUD/menu/death gates; setting bounds; progress intervals and caps. They do not
invoke a game entry point, open a disc/save or initialize audio, graphics or input.

Developer invocation, using the matching runtime and an already built private
game assembly, with every build output outside this source repository:

```text
DOTNET build tests/Focused.csproj -c Release -m:1 --artifacts-path SCRATCH_DIRECTORY -p:RuntimeDir=RUNTIME_BIN -p:GameAssembly=PRIVATE_KINGSFIELD2_DLL -p:ImplementationDir=DOTNET10_IMPLEMENTATION_DIRECTORY
DOTNET SCRATCH_DIRECTORY/bin/Focused/release/Kf2XpBarFocused.dll
```

The focused checks passed serially with one .NET core, blank RAM, and absent
audio/output and graphics-host assertions before and after. The launcher-built
game assembly requires matching .NET implementation references; use its SDK's
`shared/Microsoft.NETCore.App/10.x` folder for ImplementationDir. Native settings
integration and rendered thickness/colour/bottom alignment still need observation;
packet inspection does not establish visual acceptance.

Research and runtime: [Lexer's Verdite2 fork](https://github.com/Lexer-Lux/verdite2)
and [RecompOne](https://github.com/BlackLabelHQ/RecompOne). No disc data or generated
game code is included.
