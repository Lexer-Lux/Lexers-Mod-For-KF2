# Better Poison

Independent source mod for Japanese King's Field II (SLUS-00158).
Requires Lexer's Verdite2 fork v0.5.3-lexer.2 or later, with its shared NativeNotifications API.
Poison messages are optional in the native mod gear settings. Poison tick
damage uses the original full-screen wash with green substituted for red;
ordinary damage keeps its native color. No custom text overlay.

Native-loader and all-mods checks passed both original-font messages, a green
poison wash, preserved red ordinary damage, save/load reset and independent
disabling. Zero-damage and ignored-damage calls preserve an existing poison
flash. The native gear settings were rendered and inspected at 800x600 and
1280x720; gameplay appearance still needs an ordinary play check.
