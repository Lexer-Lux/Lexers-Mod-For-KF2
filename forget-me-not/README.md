# Forget Me Not

Independent source mod for Lexer's Verdite2 fork, Japanese King's Field II
(English SLUS-00158). Enable or disable it in Lexeditor's Mods tab.

Unchecked lootable containers and ordinary doors receive a brief white surface
glint. A locked interaction turns the glint red. Searching an empty container,
taking its contents, or successfully opening a door removes its glint. Opening a
container that still holds an item leaves it marked. The mod does not open,
unlock, or collect anything itself.

The glint is a small white or red pixel star, drawn with native world primitives.
Its anchor comes from the current posed model's visible surface, including doors
whose origin is at a hinge and containers with hollow geometry. The star twinkles
between three and nine native pixels across. It does not tint the object.
Successfully using the matching key changes red back to white immediately,
before opening. Opening a container with contents still present keeps it white;
declining a pickup or having a full inventory leaves it marked.

Explicit native packet depths let scene geometry occlude the star. Drawing
requires the fork's normal C# model walk, enabled Z-buffer and native packet-depth
tracking. Unsupported drawing paths omit the star. Secret-door behaviour and
unclassified objects are excluded; no marker is placed on walls or map tiles.
Hidden wall definitions `082`/`0CD`, unused pedestal `089`, and Phantom Rod
compartment kind `5` are excluded. Native-verified barred gate `084` is the only
eligible kind-`2` definition; ordinary kind-`3` doors remain eligible. Ordinary
static barrels/searchable props of kinds `9`/`21` remain eligible even with native
behaviour `FF`: interaction dispatch uses the definition kind. Unclassified
objects remain excluded; behaviour `FF` alone does not identify a secret.
Each star corner follows the actual model surface depth. A one-native-unit depth
tolerance prevents coplanar rounding from hiding the star on a curved lid.

Exploration knowledge is stored beside the port's settings in
`mod-data/lexer.kf2.forget-me-not.json`. A successful memory-card save records it;
loading restores only knowledge matching that card path, slot, and exact saved
game data. New Game starts fresh. The sidecar keeps the original and newest
snapshot per card/slot, at most twelve snapshots and eight MiB overall. It never
edits the game save. Older unmatched snapshots start unchecked. Existing native
opened-and-empty chest state is also respected.
New saves use the slot confirmed by the successful native write, including when
the game chooses the first free slot. Failed card writes publish no exploration
snapshot.

Disabling removes hooks and clears owned packet-depth hints. The sidecar remains
so re-enabling and loading a matching save can restore knowledge. Geometry scratch
storage and per-frame primitive allocation have fixed limits; both native
primitive cursors advance together. No mesh cache survives a game or area change.

This sprite revision passed focused geometry, state and native packet checks
against the private compiled release4 game assembly. Representative hidden native
renders were inspected for chest front/side glints, a retail locked chest, an
opened chest with unclaimed contents, an actual native pickup, an ordinary
opening door, wall occlusion, and disable/re-enable. This is representative
coverage, not a claim that every container mesh or door definition was checked.
The newly classified `084` gate and original wooden barrel `179` were also checked
with all ten authored mods enabled on the private renderer candidate. The gate's
white glint cleared after native opening. Searching an empty barrel showed the
native EMPTY message and cleared that barrel while its unchecked neighbor kept
its white glint. The native player floor and half were verified before capture.
`tests/GeometryChecks.cs.txt` contains isolated surface-anchor fixtures;
`tests/NativePacketChecks.cs.txt` checks packet bounds, depth registration, colour
and cleanup inside an isolated loaded-game probe. `tests/StateChecks.cs.txt`
checks exploration transitions and exclusions using reversible private RAM
fixtures, separately from actual native interaction checks. These files are not
compiled as part of the mod and do not establish visual acceptance.

Run the fixtures with `python tests/verify_native.py --sdk <private-game-build>
--runtime <installed-bin> --dotnet <dotnet-10-executable>`. The runner creates and
removes its own temporary build/data directory, uses one CPU, never boots the
game, and verifies that audio remains uninitialized before and after the checks.

Built on the mod API and documented game routines of
[Verdite2](https://github.com/Lexer-Lux/verdite2) and
[RecompOne](https://github.com/BlackLabelHQ/RecompOne).
No game bytes or generated game code are included.
