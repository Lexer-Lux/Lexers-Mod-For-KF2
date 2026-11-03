# Better UI

Independent source mod for Japanese King's Field II (SLUS-00158).
Shows the actual item's name in the native bottom-screen text during pickup
inspection, before the player decides to take it or put it away. Gold keeps
its native amount message. Names come from the running game's own font table.
Poison feedback belongs to the separate Better Poison mod.

Requires Lexer's Verdite2 fork v0.5.3-lexer.2 or later, with its shared
NativeNotifications API. Rejected lock and quest guards never enqueue a name;
the hook runs when the original pickup presentation actually starts.

Actual native-loader and all-mods checks passed automatic Fire Crystal pickup,
original-font name rendering, shared XP/notification packet allocation and
independent disabling. Gameplay appearance still needs an ordinary play check.

A separate native confirmation check held the inspected Fire Crystal for
2.5 seconds without confirming. The original font rendered its name with
nonzero brightness while inventory was still zero and the ground record still
contained the item. Taking then acquired it; putting away did not. Disabling
Better UI removed the name while leaving the native pickup choices working.
