# Contributing

Build and run `scripts/test.ps1` on Windows before proposing a change. Run `scripts/test.ps1 -Live -SkipBuild` after changing rendering, native window handling, or tray commands. This opens an ephemeral test cat and window and exits automatically.

Behavior belongs in CatBrain.cs and should be deterministic with a seeded Random. Avoid desktop-wide hooks or dependencies for features the existing WinForms host can implement. Preserve mute, pause, toy removal, and click-through controls.

New poses use the 48x52 pixel canvas in ExtraArt.cs. Preserve Bob's ginger stripes, cream cheeks, pink nose, and pixel outlines. `Bob The Cat.exe --preview "path-to-folder"` exports an action contact sheet and individual frames.

Please include a description of the observable change and checks performed. For bugs, include Windows version, display scaling/monitor layout, reproduction steps, and relevant lines from errors.log. Redact personal paths and window information before sharing logs.

Retain LICENSE and asset credits. Do not commit private reference photos, settings, logs, generated response files, or binaries. Release binaries are produced by the workflow or package script.
