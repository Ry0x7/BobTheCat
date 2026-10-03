# Meow recordings

Four recordings of the cat Emi by Joseph SARDIN, released under CC0 (public domain) on BigSoundBank:

- Little meow of a cat #2: https://bigsoundbank.com/little-meow-of-a-cat-2-s1472.html
- Little meow of a cat #3: https://bigsoundbank.com/little-meow-of-a-cat-3-s1473.html
- Little meow of a cat #4: https://bigsoundbank.com/little-meow-of-a-cat-4-s1474.html
- Little meow of a cat #6: https://bigsoundbank.com/little-meow-of-a-cat-6-s1476.html

Original MP3s are preserved in source/audio-source. Bundled WAVs trim surrounding silence, normalize to a quiet -26 LUFS with a -9 dB peak ceiling, and gently fade in. No pitch shifting is used. Format: mono 22050 Hz, 16-bit PCM.

Run source/prepare_meows.ps1 with FFmpeg to rebuild them. source/make_sounds.py generates only the original retro chirp. No purr recording is included.
