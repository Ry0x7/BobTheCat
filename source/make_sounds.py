"""Create the retro chirp. Recorded meows are rebuilt by prepare_meows.ps1."""
from pathlib import Path
import math
import struct
import wave

root = Path(__file__).resolve().parent / 'sounds'
root.mkdir(exist_ok=True)
rate = 22050
for name, duration in [('chirp', .24)]:
    phase = 0.0
    samples = []
    for i in range(round(duration * rate)):
        t = i / rate
        u = t / duration
        envelope = min(1, t / .04, (duration-t) / .08)
        frequency = 620 + 680 * u
        phase += 2 * math.pi * frequency / rate
        value = .10 * envelope * (math.sin(phase) + .2 * math.sin(3*phase)) / 1.2
        samples.append(round(max(-1, min(1, value))*32767))
    with wave.open(str(root / (name+'.wav')), 'wb') as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(rate)
        output.writeframes(struct.pack('<%dh' % len(samples), *samples))
print('Generated chirp WAV; recorded meows preserved')
