# Better Crystals

Before the game's Dragon Crystal revival consumes anything, choose **Revive**
or **Reload**. The original game font and confirmation boxes show your current
crystal count, current keyboard bindings and the selected digital/analog pad
bindings. Large custom binding sets cycle labelled pages.

Revive executes the original consumption, recovery and revival destination.
Reload uses the game's last successfully saved or loaded slot, even if the
port's automatic-reload setting pins another slot. It calls the native loader;
it never saves. No previous save or a failed card read leaves the choice open
with an error and the crystal unconsumed.

Requires Lexer's Verdite2 v0.5.3-lexer.4. The mod temporarily holds automatic
reload only for deaths eligible for the game's actual Dragon Crystal branch.
Other deaths and the earlier special native death branch keep their behavior.
Disabling removes that hold and returns the pending death to native behavior.
No settings, disc, card or executable patch is written.

## Verification

The focused project exercises eligibility, reload gating, earlier death ticks,
invalid slots and configured primary/secondary keyboard plus digital/analog pad
labels using blank private RAM without game, window, audio or disc startup.
Pass matching installed `RuntimeDir`, private `GameAssembly` and the .NET10
`ImplementationDir`; direct build output/intermediates to a temporary directory.

Installed native-loader checks also render the original choice, hold three
crystals before consumption, revive with exactly two remaining and restore full
HP at the native destination. The alternate choice enters the actual save
loader with three still present and restores the saved character/location.
Copied cards and the baseline remain unchanged. These use private eligibility
fixtures and scripted inputs; physical controller feel is a separate check.
