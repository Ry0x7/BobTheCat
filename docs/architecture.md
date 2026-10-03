# Architecture

Bob is a native Windows desktop application targeting .NET Framework 4.8. It uses WinForms and Win32 interop, with no NuGet dependencies or runtime network/AI service.

## Source ownership

| File | Owns |
| --- | --- |
| `source/CatBrain.cs` | Deterministic activity planning, movement, cursor curiosity, window behavior, and toy physics |
| `source/Bob.cs` | WinForms host, preferences, native rendering, tray controls, sound, and test/preview entry points |
| `source/HoverPetting.cs` | Vertical cursor strokes, head/belly targeting, and cooldowns |
| `source/ExtraArt.cs` | Embedded action/yarn frame extraction and laser target |
| `source/ToyOverlay.cs` | Separate click-through toy overlays and native resource cleanup |
| `source/AssemblyInfo.cs` | Application identity and version |

The host supplies desktop observations to the behavior engine. Keep new behavior deterministic using a seeded `Random`, so it can be exercised without a visible desktop.

## Rendering and assets

Transparent layered windows display Bob's pixel frames using System.Drawing and nearest-neighbor sampling. `Bob.png` provides the base sprites; `action-sprites.png` contains ten six-frame actions in 192x208 cells; `yarn-sprites.png` contains six 48x48 frames. The build embeds the sprite atlases, icon, and WAV effects into the executable.

Toy overlays are small windows configured to pass clicks through without activation. Bob uses open-window geometry for perches and grips, with monitor-working-area bounds and fallback behavior when a window disappears or is maximized. He does not modify window contents.

## Preferences and sound

XML preferences are stored at `%LOCALAPPDATA%\BobDesktopPet\settings.xml`; runtime errors are written to `errors.log` in the same folder. Earlier desktop-pet settings are imported when no Bob settings exist.

Sound uses four recorded CC0 meows and a generated retro chirp. Preserve mute and cooldown behavior; see `AUDIO_CREDITS.md` for sources. Asset generation is a development workflow; the shipped app uses the embedded files offline.

## Build and verification

PowerShell compiles the sources with the Windows .NET Framework compiler. `scripts/test.ps1` runs silent deterministic checks, while its `-Live` option additionally exercises layered windows and tray/overlay behavior on an interactive Windows desktop. See [development.md](development.md) for commands and packaging.
