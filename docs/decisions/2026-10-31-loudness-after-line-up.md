# Loudness after the line-up

`mediaqc check` on make-samples.sh's programme measured the whole file's loudness, line-up tone included: a 1 kHz tone
at -18 dBFS pulled it to -18.1 LUFS, so every delivery with line-up tone would fail EBU R128.

Question: whether the loudness figure includes the line-up tone.

Choices:

1. Exclude the line-up (recommended): measure from the end of the line-up tone, as programme loudness is meant; the
   report says from where.
2. The whole file: files delivered with line-up fail loudness.
3. A flag: the whole file by default, with `--skip-lineup`.

Answer: 1. The line-up is the first tone segment when only silence comes before it (`LineUp.End`); loudness is measured
from its end, as `ffmpeg -ss END -i FILE -af ebur128=peak=true` measures it, and the report has a `loudness.from`
finding with the timecode. Silence after the line-up stays in: it is below ebur128's absolute gate, so it changes
nothing.
