# Better UI

Independent source mod for Japanese King's Field II (SLUS-00158).
Shows item names in the original pickup notification presentation; gold keeps
its native amount message. Names come from the running game's own font table.
Poison feedback belongs to the separate Better Poison mod.

Requires Lexer's Verdite2 fork v0.5.3-lexer.2 or later, with its shared
NativeNotifications API. Rejected lock and quest guards never enqueue a name;
the hook runs when the original pickup presentation actually starts.

Actual native-loader and all-mods checks passed automatic Fire Crystal pickup,
original-font name rendering, shared XP/notification packet allocation and
independent disabling. Gameplay appearance still needs an ordinary play check.
