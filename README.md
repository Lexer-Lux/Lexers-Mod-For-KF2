# Lexer's Mod for King's Field II

Independent source mods for Japanese King's Field II, using the English
SLUS-00158 disc and [Lexer's Verdite2 fork](https://github.com/Lexer-Lux/verdite2/releases/tag/v0.5.3-lexer.2).
Install through Lexeditor's Mods tab; enable each module independently.
Poison, hit feedback and sprint options are in the native Mods menu's gear.
Controls and view distance are native game settings, separate from these mods.

## Features

- Better UI: item names in the original pickup presentation.
- Better Poison: optional poison/recovery messages and green poison damage flashes.
- Hit Feedback: red enemy flashes proportional to damage, with adjustable maximum duration.
- Forget Me Not: white unchecked containers and real doors, red locked attempts, cleared after searching or opening.
- Auto Pick Up All: collect loose items and gold by walking over them, preserving native capacity and quest rules.
- Better Sprint: configurable movement-only power drain, with sprint blocked at zero power.
- XP Bar: a thin full-width progress bar flush with the bottom of the game picture.
- Fully customizable -- pick and choose which modules you want!

## Requirements and use

Use fork v0.5.3-lexer.2 or later. Better UI and Better Poison use its shared
original-font notification API. No ROM, generated game code or game executable is
included. Disabling a mod removes its runtime hooks; already collected items and
ordinary saved gameplay remain saved. Forget Me Not keeps bounded exploration
state separately for each matching memory-card save.

Native-loader, game-routine, all-seven-mod integration and private save/load
checks passed. Setup and gear controls were rendered offscreen at 800x600 and
1280x720. Visible gameplay appearance and physical controller feel still need
an ordinary play check for this preview.

Built on [Verdite2](https://github.com/Lexer-Lux/verdite2) and
[RecompOne](https://github.com/BlackLabelHQ/RecompOne), both MIT.
