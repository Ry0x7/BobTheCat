param([switch]$Live, [switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$bobRoot = Split-Path -Parent $PSScriptRoot
if (-not $SkipBuild) { & (Join-Path $bobRoot 'build.ps1') }
$bobExe = Join-Path $bobRoot 'Bob The Cat.exe'
if (-not (Test-Path -LiteralPath $bobExe)) { throw 'Build Bob before running tests.' }
$bobReports = Join-Path $bobRoot 'validation'
New-Item -ItemType Directory -Path $bobReports -Force | Out-Null
$bobModes = @('self-test')
if ($Live) { $bobModes += 'smoke-test' }
foreach ($bobMode in $bobModes) {
    $bobReport = Join-Path $bobReports ($bobMode + '.txt')
    $bobProcess = Start-Process -FilePath $bobExe -ArgumentList @('--' + $bobMode, ('"' + $bobReport + '"')) -WindowStyle Hidden -PassThru
    if (-not $bobProcess.WaitForExit(30000)) { $bobProcess.Kill(); throw ($bobMode + ' timed out.') }
    $bobProcess.Refresh()
    if ($bobProcess.ExitCode -ne 0) { throw ($bobMode + ' failed. See %LOCALAPPDATA%/BobDesktopPet/errors.log.') }
    Get-Content -LiteralPath $bobReport
}
