# Forget Me Not

Independent source mod for Lexer's Verdite2 fork, Japanese King's Field II
(English SLUS-00158). Enable or disable it in Lexeditor's Mods tab.

Unchecked lootable containers and ordinary doors receive a brief white surface
glint. A locked interaction turns the glint red. Searching an empty container,
taking its contents, or successfully opening a door removes its glint. Opening a
container that still holds an item leaves it marked. The mod does not open,
unlock, or collect anything itself.

The glint uses the model's native lighting for a short pulse every 1.8 seconds.
It follows the world's depth and visibility rules. Secret-door behaviour and
unclassified objects are excluded; no marker is placed on walls or map tiles.

Exploration knowledge is stored beside the port's settings in
`mod-data/lexer.kf2.forget-me-not.json`. A successful memory-card save records it;
loading restores only knowledge matching that card path, slot, and exact saved
game data. New Game starts fresh. The sidecar keeps the original and newest
snapshot per card/slot, at most twelve snapshots and eight MiB overall. It never
edits the game save. Older unmatched snapshots start unchecked. Existing native
opened-and-empty chest state is also respected.

Disabling removes hooks and restores any temporary model-lighting change. The
sidecar remains so re-enabling and loading a matching save can restore knowledge.

The implementation has passed isolated native-loader and game-routine checks.
Glint appearance still needs a person to judge in the rendered game. See
`VERIFICATION.md` for measured coverage and remaining checks.

Built on the mod API and documented game routines of
[Verdite2](https://github.com/Lexer-Lux/verdite2) and
[RecompOne](https://github.com/BlackLabelHQ/RecompOne).
No game bytes or generated game code are included.
