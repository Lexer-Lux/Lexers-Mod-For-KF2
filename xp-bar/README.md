# XP Bar

Independent source mod for Lexer's Verdite2 fork: Japanese King's Field II,
English disc SLUS-00158. Enable it in the native Mods menu or through Lexeditor's
Mods tab once that integration is delivered. There are no settings to configure.
The folder alone does not enable it in a running game.

A muted gold fill sits on a dark, two-game-pixel track flush with the bottom of
the game picture. The track reaches both absolute picture edges at 4:3 and wider
aspects. It has no inset, border, text or interactive overlay. It uses ordinary
native POLY_F4 packets in the game's ordering table, behind screen tints.

Progress is `(current XP - this level's starting XP) / (next level's XP - starting XP)`.
Current XP, next XP and level are read from the running game. The previous
threshold comes from the game's loaded level table; levels beyond the table use
the final interval, as the native level-up routine does. The XP or level cap shows
a complete bar. Values outside an interval are clamped without changing game data.

The bar follows native HUD visibility, hides on loading/title/death screens and
world redraws behind native menus or messages, and reads fresh state every draw.
It keeps no save-specific progress cache. Disabling removes its single hook and
leaves the game's character data unchanged.

## Verification

The real native loader in the installed published fork compiled and loaded the
mod with one hook and no empty settings button. An isolated copied-save session
called the original native level-up routine: level 1 became 2, with the native
previous/next thresholds 50/110 matching the table indexes. Progress reset to zero;
a private 80-XP fixture then produced half a level. No player save was written.

The native ordering-table renderer processed the full-width bottom track in a
headless 16:9 run. Opening the real native menu exercised the hidden-bar path;
closing it and disabling the mod produced no further XP packets. Separate packet
checks fed its native packets through the GPU command parser at 4:3, 16:9 and
21:9, verifying negative left coordinates, both absolute edges, bottom alignment,
track/fill order, and bounded primitive allocation. Fraction fixtures cover a new
character, ordinary level changes, post-table levels, clamping and caps.

These checks establish the mechanism and coordinates. The bar's rendered color,
thickness and appearance still need an ordinary visual play check.

Research and runtime: [Lexer's Verdite2 fork](https://github.com/Lexer-Lux/verdite2)
and [RecompOne](https://github.com/BlackLabelHQ/RecompOne). No disc data or generated
game code is included.
