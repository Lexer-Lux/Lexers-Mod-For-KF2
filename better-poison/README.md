# Better Poison

Independent source mod for Japanese King's Field II (SLUS-00158).
Requires Lexer's Verdite2 fork v0.5.3-lexer.2 or later, with its shared NativeNotifications API.
Poison messages are optional in the native mod gear settings. Poison tick
damage uses the original full-screen wash with green substituted for red;
ordinary damage keeps its native color. No custom text overlay.

The gear exposes two independent visual settings:

- **Flash peak intensity (%)**,0–100, defaults to35% of the native full-scale
  tint coefficient. The native routine multiplies RGB by a Q12 coefficient
  (4096=full intensity), not a primitive opacity. Its original2400 is58.59375%.
  The new peak rounds to1434/4096 (35.01%), producing green21 instead of the
  original red35 from RGB60 at the nominal peak. Actual composition remains
  the game's screen-tint renderer. Changing intensity never changes frequency.
- **Flash frequency (Hz)**,0–0.5, defaults to0.25Hz: one flash every4seconds of
  world ticks. Zero hides poison flashes. The native poison counter at80199468
  decreases at15Hz; multiples of30 cause1HP damage and reset the original wash
  to2400, with60 coefficient units removed per world tick. This gives native damage/flash
  opportunities every2seconds (0.5Hz), with a nominal40-tick/2⅔-second wash.
  Visual opportunities are gated, never damage. Intermediate frequencies are
  distributed over those2-second opportunities, so individual intervals are
  quantized to2seconds. No unsupported faster frequency is offered.

The first eligible poison event flashes immediately. A skipped event does not
restart or recolor the prior poison envelope. The visual envelope still lasts
40world ticks, with peak intensity scaled independently. Menus pause its world-tick
clock. A hidden poison wash skips the native averaging contribution entirely,
so it cannot dim simultaneous unrelated tints. HP damage, poison duration and
recovery remain native.

The native hook manager calls mod prefixes/postfixes even when frame pacing skips
the player stage. The post-player observer checks FramePacing.TickedThisFrame
after the pacing prefix's final normal/fallback decision, so render-only frames
do not shorten the world-tick envelope. A late native poison event keeps the
full40-tick envelope through that post; the next native tint uses the full peak.

Notifications compare poison state only during continuous living play. Current
HP is80199428 (80199426 is maximum HP). Death, save unpack and native full-status
reset/respawn cancel owned notices and discard the previous poison state, so
clearing status through those paths cannot produce a false “POISON CURED”. An
antidote/spell or natural expiry during continuous play still produces the cure
notice when notices are enabled.

A status transition cancels only this mod's previous queued/displayed status
before posting the current one. Recovery therefore replaces stale POISONED with
POISON CURED; pickup/gold messages and other owners keep their queue entries and
order. Turning notices off dismisses this owner's current notice as well.

Native-loader and all-mods checks passed both original-font messages, a green
poison wash, preserved red ordinary damage, save/load reset and independent
disabling. Zero-damage and ignored-damage calls preserve an existing poison
flash. The native gear settings were rendered and inspected at 800x600 and
1280x720; these historical checks predate the new intensity/frequency settings.
The expanded focused check is `tests/verify_native.py --sdk PRIVATE_GAME_SDK_BIN
--runtime INSTALLED_RUNTIME_BIN --dotnet DOTNET10_EXE`. It uses private settings/RAM and the actual notification
queue, with no game entry point, disc, save, window or audio. It covers cadence,
intensity, other damage, notification toggling and reset-versus-cure transitions.
The expanded check passed against the installed release4 private game assembly,
including headless Audio.Initialize preflight and post-check proof that device,
context and mixer stayed null and the source stayed zero. Ordinary gameplay
controller feel remains outside that check.

Silent installed-native checks subsequently loaded all9hooks and observed the
native wash rendering. Old-strength settings produced green35; the35% default
produced green21. Default pulse starts remained60world ticks apart at both30
and60renderFPS (measured4.029s and4.024s), with native HP damage still every30
world ticks. The40-state-tick envelope was unchanged by the render rate; the
native integer RGB quantization can make its final tiny coefficient invisible.
Zero-contribution tints are skipped instead of diluting another tint's average.

Actual consumable cure, natural expiry, death/reset and private slot1 loading
were exercised. The final owner-replacement check observed native tint arguments
`0,60,0,1434` at the first peak and a rendered original-font **POISON CURED**
replacing the older POISONED status. Disabling restored native red poison flashes
and emitted no notices. Native menu/death/reset entry-point checks do not claim
a complete human playthrough; the scripted physical input checks do not measure
human controller feel.
