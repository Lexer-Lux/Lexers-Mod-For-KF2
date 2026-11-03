# Better Sprint

Independent source mod for Lexer's Verdite2 fork: Japanese King's Field II,
English disc SLUS-00158. Enable or disable it in the native Mods menu or through
Lexeditor's Mods tab once that integration is delivered. It is not deployed to
the player's active game by this folder alone.

The mod gear exposes **Stamina drain (%/second)**, from 0 to 300. The default is
**150**, the measured vanilla rate: 500 points per 15 Hz world tick from each
5000-point physical and magic power gauge. There is no new stamina pool. Both
existing gauges retain their vanilla relationship; ordinary attacks, spells,
recovery and exhaustion still use the game's own routines.

Running drains only when the player actually changes horizontal position after
collision resolution. Holding Run while stationary, turning in place, or pushing
against a wall costs nothing. Empty physical power blocks running and removes
carried sprint acceleration while preserving negative environmental slowdowns.
Walking remains available. Drain is independent of rendering frame rate.

The mod works around the game's native run routine after the fork translates its
separate Run binding. It does not change bindings or implement another controls
layout. Disabling it removes its hooks and restores native drain, including
vanilla's drain while standing with the action button held. A saved drain setting
is read again on the next enable. Loading a save clears fractional drain and any
unfinished movement observation.

## Verification

An isolated headless session used the installed published fork and a copied save;
no live player data or game executable was modified. Its real native mod compiler
loaded all seven hooks. Measurements on actual world ticks confirmed:

- Unchanged position: no drain.
- Actual movement: 500 points at the default rate.
- Saved setting changed to 75, then native disable/re-enable: 250 points while moving.
- Physical power forced to zero with a carried positive run ramp: native walk
  speed remained 200, with the run ramp zero; walking and native recovery continued.
- Native disable: 500-point vanilla drain returned, including after movement stopped.

A separate nine-assertion probe called the original game run routine and checked
clamping, scoped input restoration, separate-run composition and unchanged vanilla
behavior without the hooks. Native gear settings were rendered and inspected at
800x600 and1280x720. Physical-controller feel still needs an ordinary play check.

Research and runtime: [Lexer's Verdite2 fork](https://github.com/Lexer-Lux/verdite2)
and [RecompOne](https://github.com/BlackLabelHQ/RecompOne). This folder contains no
disc data or generated game code.
