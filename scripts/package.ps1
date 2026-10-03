param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$bobRoot = Split-Path -Parent $PSScriptRoot
if (-not $SkipBuild) { & (Join-Path $PSScriptRoot 'test.ps1') }
$bobDist = Join-Path $bobRoot 'dist'
$bobPortable = Join-Path $bobDist 'Bob The Cat'
New-Item -ItemType Directory -Path $bobPortable -Force | Out-Null
foreach ($bobName in @('Bob The Cat.exe','Bob The Cat.exe.config','README.md','LICENSE','CREDITS.md','AUDIO_CREDITS.md')) {
    Copy-Item -LiteralPath (Join-Path $bobRoot $bobName) -Destination $bobPortable
}
Copy-Item -LiteralPath (Join-Path $bobRoot 'docs') -Destination $bobPortable -Recurse -Force
$bobZip = Join-Path $bobDist 'Bob The Cat.zip'
Compress-Archive -LiteralPath $bobPortable -DestinationPath $bobZip -Force
Write-Output $bobZip
