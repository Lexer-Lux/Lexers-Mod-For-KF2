# Hit Feedback

Independent C# source mod for Lexer's Verdite2 fork, Japanese King's Field II
(English disc SLUS-00158). Enable or disable it through the Mods tab.

Enemies flash red after losing HP. Each hit lasts:

`maximum flash duration × min(HP actually lost / enemy maximum HP, 1)`

The native mod gear exposes **Maximum flash duration (seconds)**, from 0 to 5,
initially 0.60 seconds. Zero suppresses the flash. Healing does not flash.
Overlapping flashes remain visible until the later expiry. Linked body parts
use their parent's HP and flash together. Spawn, save load and area changes
clear old flashes.

Maximum HP comes from the game's live creature definition, the same field its
spawn routine uses. Red lighting applies only while that creature's model is
submitted, through the game's existing lighting. The GPU retained renderer and
the native polygon path read the same modified values; the original lighting is
restored immediately afterward. This mod adds no overlay and alters no game file.

Private headless checks passed the real native loader, native damage and spawn
routines, proportional duration, cap, zero duration, healing, linked body parts,
retained model capture, exact lighting restoration, load reset and reversible
native disable. The visible colour and timing during ordinary combat still need
human acceptance; headless counters do not establish picture quality.

Research: [Verdite2](https://github.com/Lexer-Lux/verdite2) and
[RecompOne](https://github.com/BlackLabelHQ/RecompOne), both MIT.
No disc data or generated game code is included.
