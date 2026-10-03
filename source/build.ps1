$ErrorActionPreference = 'Stop'
$bobRoot = Split-Path -Parent $PSScriptRoot
$bobCompiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path -LiteralPath $bobCompiler)) {
    $bobCompiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe'
}
if (-not (Test-Path -LiteralPath $bobCompiler)) { throw 'The Windows .NET Framework compiler is unavailable.' }
$bobResponse = Join-Path $PSScriptRoot 'build.rsp'
$bobArguments = @(
    '/nologo', '/target:winexe', '/optimize+', '/platform:anycpu',
    ('/win32icon:"{0}"' -f (Join-Path $PSScriptRoot 'Bob.ico')),
    ('/win32manifest:"{0}"' -f (Join-Path $PSScriptRoot 'Bob.manifest')),
    ('/resource:"{0}",Bob.spritesheet.png' -f (Join-Path $PSScriptRoot 'Bob.png')),
    ('/resource:"{0}",Bob.action-sprites.png' -f (Join-Path $PSScriptRoot 'action-sprites.png')),
    ('/resource:"{0}",Bob.yarn-sprites.png' -f (Join-Path $PSScriptRoot 'yarn-sprites.png')),
    ('/resource:"{0}",Bob.ico' -f (Join-Path $PSScriptRoot 'Bob.ico')),
    ('/resource:"{0}",Bob.meow.wav' -f (Join-Path $PSScriptRoot 'sounds/meow.wav')),
    ('/resource:"{0}",Bob.chirp.wav' -f (Join-Path $PSScriptRoot 'sounds/chirp.wav')),
    ('/resource:"{0}",Bob.meow2.wav' -f (Join-Path $PSScriptRoot 'sounds/meow2.wav')),
    ('/resource:"{0}",Bob.meow3.wav' -f (Join-Path $PSScriptRoot 'sounds/meow3.wav')),
    ('/resource:"{0}",Bob.meow4.wav' -f (Join-Path $PSScriptRoot 'sounds/meow4.wav')),
    '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll',
    ('/out:"{0}"' -f (Join-Path $bobRoot 'Bob The Cat.exe')),
    ('"{0}"' -f (Join-Path $PSScriptRoot 'AssemblyInfo.cs')),
    ('"{0}"' -f (Join-Path $PSScriptRoot 'CatBrain.cs')),
    ('"{0}"' -f (Join-Path $PSScriptRoot 'ExtraArt.cs')),
    ('"{0}"' -f (Join-Path $PSScriptRoot 'ToyOverlay.cs')),
    ('"{0}"' -f (Join-Path $PSScriptRoot 'HoverPetting.cs')),
    ('"{0}"' -f (Join-Path $PSScriptRoot 'Bob.cs'))
)
$bobArguments | Set-Content -LiteralPath $bobResponse -Encoding UTF8
& $bobCompiler ('@' + $bobResponse)
if ($LASTEXITCODE -ne 0) { throw 'Bob build failed.' }
Write-Output (Join-Path $bobRoot 'Bob The Cat.exe')
