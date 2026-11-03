# Red N

Independent native source mod for Japanese King's Field II, English SLUS-00158.
The native Mods toggle enables it; there are no additional settings.

Colors only the compass **N** red. Its red brightness and existing vertex
lighting remain native. Other cardinal letters, the compass sphere, HP/MP,
gauges, menus and world geometry retain their colors. Disabling the mod leaves
the next native HUD build untouched; no textures or model data need restoring.

The compass is HUD record0 at80067774, model0: a26-vertex,48-face textured
sphere. Its N occupies a separate cell in the original shared HUD atlas:
texturepage9, CLUT7B40, UVu0..63/v128..191. Exactly four of the model's textured
quads (faces5,6,9,10) sample that cell. E uses the adjacent cell; S/W are below.
The atlas was reconstructed offline from the original common FDAT47 upload data
and visually inspected. No game assets are included here.

The mod observes only the new packet span from native HUD builder80031D5C. It
accepts the original GT4 command, texturepage, palette and N-cell UV bounds;
then zeros green/blue modulation on those vertices. This recolors only N's
nontransparent texels, retaining its red shading. It never changes the shared
palette, the VRAM atlas, or the whole compass color. Invalid packet spans,
changed descriptors, unknown layouts and more than four matches are left alone.

Focused packet-buffer checks passed with zero build warnings/errors. They verify
that only N's green/blue bytes change, preserve every other packet byte, and
exercise hidden/disabled/malformed-span guards. A read-only private original RAM
fixture confirmed26vertices/48faces and exactly faces5,6,9,10 matching the N cell.
Run `tests/verify_native.py --sdk PRIVATE_GAME_SDK_BIN --runtime INSTALLED_RUNTIME_BIN
--dotnet DOTNET10_EXE --model-ram PRIVATE_EXISTING_RAM_FIXTURE` to repeat. Build,
settings and private RAM are isolated in a temporary directory; headless audio
preflight and post-check prove null device/context/mixer and a zero source.

The check constructs packet buffers using the native packet layout and original
mesh UVs; it does not boot the game or evaluate GPU gameplay appearance. Native
loader registration and ordinary gameplay appearance acceptance remain pending.
Development notes and canonical game knowledge belong to Lexeditor, not this
distribution catalog.
