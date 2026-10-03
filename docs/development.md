# Development, testing, and packaging

## Requirements

Use Windows 10/11, Windows PowerShell, and .NET Framework 4.8. The build locates the framework's `csc.exe` under the Windows directory; no .NET SDK or NuGet install is required. Keep all checked-in source assets available. Python and FFmpeg are only needed when regenerating audio.

## Build

Run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

The root script delegates to `source/build.ps1`, which writes `Bob The Cat.exe` to the repository root and embeds the sprites, icon, and sounds. Launch the executable with `Bob The Cat.exe.config` beside it.

## Test

```powershell
.\scripts\test.ps1
.\scripts\test.ps1 -Live -SkipBuild
```

The first command builds and runs silent deterministic tests, writing reports under `validation/`. The second reuses the executable and also opens a temporary cat/test window to exercise native rendering, tray controls, toy click-through, and no-activation behavior; run it on an interactive desktop after renderer or window-handling changes. Do not use `-SkipBuild` after source changes until you have rebuilt.

For manual checks, verify multiple monitors (including negative coordinates), window maximize/close, dragging, hover petting over visible fur, sound muting, pause, and toy cleanup. Preserve access to the tray menu when click-through is enabled.

## Package

After the build and checks:

```powershell
.\scripts\package.ps1 -SkipBuild
```

The portable archive is written to `dist/Bob The Cat.zip`. Keep the executable and config together when distributing. The GitHub Actions workflow builds, runs deterministic checks, and uploads the ZIP and test reports as workflow artifacts. Publishing a GitHub release attachment is a separate action; the packaging command does not publish anything.

## Troubleshooting

- If the compiler cannot be found, verify the Windows .NET Framework installation.
- If Bob is click-through, use the notification-area cat icon to change preferences; check the hidden-icons menu if needed.
- For runtime failures, inspect `%LOCALAPPDATA%\BobDesktopPet\errors.log` and the test reports. Redact personal paths and window information before sharing logs.
- Retain code and media attribution when distributing; see `LICENSE`, `CREDITS.md`, and `AUDIO_CREDITS.md`.
