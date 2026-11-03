# Hit Feedback

Independent C# source mod for Lexer's Verdite2 fork, Japanese King's Field II
(English disc SLUS-00158). Enable or disable it through the Mods tab.

Enemies receive a red tint after losing HP. Its initial strength is:

`(1 - remaining HP after the hit / enemy maximum HP) × red intensity`

The strength fades linearly to zero over this duration:

`maximum flash duration × min(HP actually lost / enemy maximum HP, 1)`

The native mod gear exposes **Red intensity (%)**, from 0 to 100, initially 50%,
and **Maximum flash duration (seconds)**, from 0 to 5, initially 0.60 seconds.
At 25% remaining HP and 50% intensity, the initial red strength is 37.5%. A
1-second maximum and a loss of 10% of maximum HP give 0.1 seconds of fading.
Zero intensity leaves native colour untouched, including an active pulse. Red
intensity changes affect an active pulse immediately; duration is chosen on each
hit, and setting its maximum to zero also suppresses active colour.

A new damaging hit interrupts and replaces that creature's previous pulse. Its
peak comes from the new remaining HP; its duration comes only from the latest
actual HP loss. Peaks are never added, and an older long pulse cannot extend a
new short pulse. Between hits the tint fades continuously; it does not toggle
between opaque red and vanilla. Healing and no-damage calls neither start nor
restart a pulse. Linked body parts use their parent's HP and share one pulse.
A killing hit can tint the death animation until its normal expiry. Spawn,
save load, new-game/area overlays and disabling clear old pulses, so another
creature cannot inherit the colour.

Maximum HP comes from the game's live creature definition, the same field its
spawn routine uses. Newly captured retained GPU instances carry the red target
and strength. The renderer mixes their final textured, lit and fogged RGB toward
pure red before quantization. Alpha, transparent-texel rejection, depth and blend
mode stay native. Native memory RGBC and GTE lighting are never changed.

This requires the fork's retained GPU geometry/model meshes and generic
`ModelInstance.ColourMixRgb` / `ColourMix` API. The mod settings identify the
requirement when it is unavailable. Packet and legacy rendering have no tint
fallback: multiplying a green texture by a red light only makes it brown/dark,
which does not implement the requested red strength. Nested draws, unrelated
objects, menu previews, first-person arms and a changed frame serial are excluded.
This mod adds no overlay and alters no game file.

Focused checks in `tests/Focused.csproj` exercise the mod against blank private
emulated RAM, without a game entry point, disc, save, audio, window or input.
They cover the 37.5% example, linear fade, exact expiry, latest-hit replacement,
linked parts, healing/no damage, bounds/invalid settings, zero intensity, native
lighting/RGBC remaining unchanged, per-instance main/mirror ranges, unrelated
and nested draws, unsupported rendering, frame changes, death, spawn, load,
new game and disable.

Developer invocation, using the matching runtime and an already built private
game assembly, with every build output outside this source repository:

```text
DOTNET build tests/Focused.csproj -c Release -m:1 --artifacts-path SCRATCH_DIRECTORY -p:RuntimeDir=RUNTIME_BIN -p:GameAssembly=PRIVATE_KINGSFIELD2_DLL -p:ImplementationDir=DOTNET10_IMPLEMENTATION_DIRECTORY
DOTNET SCRATCH_DIRECTORY/bin/Focused/release/Kf2HitFeedbackFocused.dll
```

The revised final-RGB instance checks passed against the matching candidate
runtime, serially with one .NET core, blank RAM, and absent audio/output and
graphics-host assertions before and after. The launcher-built
game assembly requires matching .NET implementation references; use its SDK's
`shared/Microsoft.NETCore.App/10.x` folder for ImplementationDir.

Requires fork v0.5.3-lexer.5. Actual native GPU captures with all ten authored mods
loaded verify original appearance at zero, textured/shaded red at37.5%, and a
pure-red100% endpoint retaining holes and silhouette. The surrounding scene stays
unchanged. An actual native damage call against the existing green plant removes
72/220HP and starts a981ms pulse with a3-second maximum; it fades back to green.
No HP writes create that damage. These private, silent checks use scripted
fixtures and do not establish physical controller feel or a full playthrough.

Research: [Verdite2](https://github.com/Lexer-Lux/verdite2) and
[RecompOne](https://github.com/BlackLabelHQ/RecompOne), both MIT.
No disc data or generated game code is included.
