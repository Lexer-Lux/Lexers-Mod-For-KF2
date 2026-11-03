# Auto Pick Up All

Independent source mod for Lexer's Verdite2 fork, Japanese King's Field II
(English SLUS-00158). Enable or disable it through Lexeditor's Mods tab.

Walk over a loose item or gold to collect it. Items retain their original pickup
presentation, which confirms automatically; gold uses its native coin message.
The mod does not open containers or take their linked contents. It preserves
native capacity limits, quest gates, special-item handling and inventory grants.

Collection checks a small horizontal radius around the player's feet and a
limited height difference. It runs only during ordinary live play. A native
refusal will not repeat while standing on the same item; step off and back on to
retry. A full inventory leaves the item where it is, and freeing capacity lets
collection work again. Loading, New Game and area changes reset retry state.

Automatic confirmation affects only the native pickup call started by this mod.
Other menus and controller input keep their normal behavior. Disabling the mod
removes its hooks and input listener; it changes no saves, files or game settings.
Items already collected remain collected, as with a normal pickup.

Native-loader and isolated game-routine checks pass. Walking feel and the rendered
pickup presentation remain for in-game visual acceptance.

Built on [Verdite2](https://github.com/Lexer-Lux/verdite2) and
[RecompOne](https://github.com/BlackLabelHQ/RecompOne)'s source-mod API.
No game bytes or generated game code are included.
