$ErrorActionPreference = 'Stop'
$meowIds = @(1472,1473,1474,1476)
for ($index=0; $index -lt $meowIds.Count; $index++) {
    $recording = Join-Path $PSScriptRoot ('audio-source/meow-' + $meowIds[$index] + '.mp3')
    $soundName = if ($index -eq 0) { 'meow' } else { 'meow' + ($index+1) }
    $output = Join-Path $PSScriptRoot ('sounds/' + $soundName + '.wav')
    & ffmpeg -hide_banner -loglevel error -y -i $recording -af 'silenceremove=start_periods=1:start_threshold=-45dB:start_silence=0.04,areverse,silenceremove=start_periods=1:start_threshold=-45dB:start_silence=0.08,areverse,loudnorm=I=-26:TP=-9:LRA=7,afade=t=in:d=0.015' -ac 1 -ar 22050 -c:a pcm_s16le $output
    if ($LASTEXITCODE -ne 0) { throw 'Meow conversion failed' }
}
