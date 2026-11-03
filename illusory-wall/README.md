# illusory wall

Independent native source mod for Japanese KF2 (US King's Field, SLUS-00158).

Three independently combinable native settings:

| Setting | Default | Behavior |
| --- | --- | --- |
| On use | On | The native use button opens a hidden wall-door. Off skips that candidate while continuing the native interaction scan. |
| On attack | Off | An actual melee hit sample on the wall starts its native opening animation. A button press without a hit does nothing. |
| Walk through | Off | Player movement can pass through its collision footprint. The wall stays visually hidden and closed; this does not permanently open or reveal it. |

Locks remain enforced. Settings keys are `lexer.kf2.illusorywall.onuse`,
`.onattack`, and `.walkthrough`. Disabling restores the game's native behavior.
The mod adds no exploration glints and does not affect ordinary doors,
Phantom Rod compartments, containers, or save points.

The native wall definitions **0x82** and **0xCD** are checked against both
definition kind 2 and live behavior 2. CD shares the original wall geometry;
its actual native render shows a continuous rock panel matching its surroundings,
which native use opens into a passage. The other kind-2 definitions are excluded:
0x84 is a visible barred gate, and 0x89 is an unused pedestal. This identification
is for the port's original SLUS-00158 data, not arbitrary replacement models.
The old renderer label “secret door” for kind 0x0E is unsuitable: that native
interaction opens the save menu.

The native use handler sets an unlocked closed kind-2 record's state to 1.
The native weapon-hit loop queries creatures; it does not open these doors.
Native door motion changes the two-cell collision footprint through `func35CC8`.
Walk-through uses that routine's own closed/open pattern, only around native
player horizontal collision and vertical floor/step calls. The opened floor is
queried while crossing so the closed wall does not make the player fall. Both
map halves are restored in `finally`, including
exception, disable, load and New Game cleanup. Area identity prevents an old
footprint from being restored into a new area. There are no direct file or card
writes; use/attack opening remains ordinary native world state for native saves.

`tests/Probe.cs.txt` is a blank-RAM focused fixture, including a comparison with
the actual native footprint routine, defaults, exclusions, hit-only activation,
locks, use filtering, temporary collision changes and exception/disable cleanup.
It explicitly suppresses audio/graphics before any initialization and asserts no
audio device/source/mixer or graphics host exists. It never boots the game.
The test project requires the matching private release-built game DLL,
`RuntimeDir`, and the .NET10 runtime's `ImplementationDir`; build artifacts belong
outside this repository. This focused fixture passed against installed release4's
runtime and matching launcher-built game assembly. It checks native footprint
application, rather than an installed loader session or rendered gameplay.
Separate private native acceptance exercised all eight setting combinations using
actual use inputs, successful melee swings/hit samples and player movement.
Matched-camera renders show walk-through keeps the wall visible and closed;
native geometry fields are restored, floor height is preserved, locked attempts
stay closed and disabling restores blocked passage. Both original wall variants
were rendered and exercised. The original barred gate remained excluded.

Identity baseline: [Acranon's Reveal](https://github.com/Acranon/verdite2-mods/blob/main/mods/reveal/Reveal.cs)
records definition 0x82's wall-door identity from play. This mod uses its own
native interaction and collision implementation.
