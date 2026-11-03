# Lexer's Mod for King's Field II

Independent source mods for Japanese King's Field II, using the English
SLUS-00158 disc and [Lexer's Verdite2 fork](https://github.com/Lexer-Lux/verdite2).
Install through Lexeditor's Mods tab; enable each module independently.
Options are in each mod's native Mods menu gear.
Controls and view distance are native game settings, separate from these mods.

## Features

- Better UI: item names in the original pickup presentation.
- Better Poison: optional current poison/recovery messages, independent green flash intensity and frequency, and no cure message on death or load.
- Hit Feedback: red enemy flashes that fade; intensity follows remaining health and duration follows the hit's actual damage.
- Forget Me Not: small white surface glints on unfinished containers and verified ordinary doors; red after locked attempts. Empty search or taking contents clears containers, successful opening clears doors.
- Auto Pick Up All: collect loose items and gold by walking over them, preserving native capacity and quest rules.
- Better Sprint: configurable movement-only power drain, optional free magic power (default on), and release-to-run again after exhaustion.
- XP Bar: configurable height, full width and flush with the absolute bottom.
- Red N: only the compass N is red.
- Better Crystals: choose native Dragon Crystal revival or reload the actual last save before consuming a crystal.
- illusory wall: independently enable native hidden-wall use, attack and collision-only walk-through.
- Fully customizable -- pick and choose which modules you want!

## Requirements and use

Use fork v0.5.3-lexer.5 or later for this complete collection. Better UI and Better Poison use its shared
original-font notification API. No ROM, generated game code or game executable is
included. Disabling a mod removes its runtime hooks; already collected items and
ordinary saved gameplay remain saved. Forget Me Not keeps bounded exploration
state separately for each matching memory-card save.

The native loader and all ten source modules were checked together in an
isolated game with copied saves and verified absent audio/physical input.
Actual native renders cover item feedback, white/red/cleared surface glints,
barrels and an ordinary barred gate, green poison and cured messages, N-only
colour, adjustable bottom XP, and a fading red enemy hit effect. Hidden-wall
options have native use, melee and collision checks for every toggle combination.
Individual READMEs describe limits and distinguish native fixtures from ordinary
play. Physical controller feel and a full playthrough remain separate checks.

Hit Feedback and Forget Me Not require the supported GPU geometry/depth path;
their READMEs describe unsupported renderer settings. Controls and native save
behavior remain the fork's settings and game routines.

Built on [Verdite2](https://github.com/Lexer-Lux/verdite2) and
[RecompOne](https://github.com/BlackLabelHQ/RecompOne), both MIT.
