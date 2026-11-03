# Better Sprint

Independent source mod for Lexer's Verdite2 fork: Japanese King's Field II,
English disc SLUS-00158. Enable or disable it in the native Mods menu or through
Lexeditor's Mods tab once that integration is delivered. It is not deployed to
the player's active game by this folder alone.

The mod gear exposes **Stamina drain (%/second)**, from 0 to 300. The default is
**25**. The measured vanilla rate is 150% per second: 500 points per 15 Hz world tick from each
5000-point physical and magic power gauge. There is no new stamina pool.
**Running consumes no magic power** defaults on: running charges only physical
power. Turn it off to charge both gauges at the selected rate. Magic power means
the existing spell-readiness/strength gauge at80199432, not MP. Spell, quick-item,
attack and recovery changes are preserved; this switch forgives only running's
own deferred charge.

Running drains only when the player actually changes horizontal position after
collision resolution. Holding Run while stationary, turning in place, or pushing
against a wall costs nothing. Empty physical power blocks running and removes
carried sprint acceleration while preserving negative environmental slowdowns.
Walking remains available. Once physical power reaches zero, holding Run cannot
restart sprint when power recovers: release Run, then press again. Both the
Classic action/run input and the fork's separate modern Run binding use this
latch. Stationary ticks and transient menus do not clear exhaustion. Death,
native full-status reset/respawn and save unpack discard pending observations
and fractional drain, and require release before rearming held Run. Disabling
the mod clears its owned state and restores native behavior. Drain is independent
of rendering frame rate.

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
- Actual movement: 1250 points per second at the default rate.
- Saved setting changed to 75, then native disable/re-enable: 250 points while moving.
- Physical power forced to zero with a carried positive run ramp: native walk
  speed remained 200, with the run ramp zero; walking and native recovery continued.
- Native disable: 500-point vanilla drain returned, including after movement stopped.

A previous nine-assertion probe called the original game run routine and checked
clamping, scoped input restoration, separate-run composition and unchanged vanilla
behavior without the hooks. Native gear settings were rendered and inspected at
800x600 and1280x720. Physical-controller feel still needs an ordinary play check.

The expanded focused check is `tests/verify_native.py --sdk PRIVATE_GAME_SDK_BIN
--runtime INSTALLED_RUNTIME_BIN --dotnet DOTNET10_EXE`. It builds outside the repository and calls the actual
native run routine in new private RAM, with no ROM, save, launcher, game loop,
native window or audio initialization. It covers both input paths, exhaustion
and release, recovery while held, menu/load/reset, stationary movement, optional
magic cost and preservation of action changes. The expanded check passed against
the installed release4 private game assembly, including a headless audio
preflight and post-check null device/context/mixer and zero source. Ordinary
gameplay acceptance remains outstanding; the historical measurements above predate the
new no-magic default and exhaustion latch.

Updated silent installed-native checks loaded all10hooks and drove remapped
keyboard and controller states through the installed input translators. Both
Classic and separate modern Run paths passed actual movement, recovery while
held, exhaustion, release and fresh-press checks. Native movement charged500 at
150%/second and250 at75%; the magic-power option worked while MP remained17.
Stationary running charged neither gauge. Disabling restored vanilla500-point
charges to both gauges while stationary. Native menu/death/status-reset entry
points and private slot1 loading preserved the required-release behavior.

The Classic fresh Use dispatch and restored action bit were observed; its
targeted fountain/item side effect was isolated in that speed fixture to avoid
an unrelated modal. These are automated input/state checks, not human controller
feel or a complete interactive menu/respawn playthrough.

Research and runtime: [Lexer's Verdite2 fork](https://github.com/Lexer-Lux/verdite2)
and [RecompOne](https://github.com/BlackLabelHQ/RecompOne). This folder contains no
disc data or generated game code.
