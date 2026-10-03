# Contributing

Build and run `scripts/test.ps1` on Windows before proposing a change. Run `scripts/test.ps1 -Live -SkipBuild` after changing rendering, native window handling, or tray commands. This opens an ephemeral test cat and window and exits automatically.

Behavior belongs in CatBrain.cs and should be deterministic with a seeded Random. Avoid desktop-wide hooks or dependencies for features the existing WinForms host can implement. Preserve mute, pause, toy removal, and click-through controls.

New action poses use 192x208 cells in `source/action-sprites.png`, extracted by `ExtraArt.cs`; yarn uses 48x48 cells. Keep one shared scale per action and nearest-neighbor rendering. Preserve Bob's ginger stripes, cream cheeks, pink nose, and pixel outlines. See [artwork notes](docs/artwork.md). `Bob The Cat.exe --preview "path-to-folder"` exports an action contact sheet and individual frames.

Please include a description of the observable change and checks performed. For bugs, include Windows version, display scaling/monitor layout, reproduction steps, and relevant lines from errors.log. Redact personal paths and window information before sharing logs.

Retain LICENSE and asset credits. Do not commit private reference photos, settings, logs, generated response files, or binaries. Release binaries are produced by the workflow or package script.
