# A WAV cut short

Asked 2026-11-06, when the backlog's Next list ran out after the start timecode and the owner moved this item up from
Maybe. decisions/2026-11-04-odd-files.md left it open: FFmpeg gives a WAV's duration from the bytes there are, not from
its header's data size, so a stem cut short in copying or export decoded to the shorter length and `decode.sound`
passed it. FFmpeg only logs "Estimating duration from bitrate, this may be inaccurate".

Found while looking: a cut RF64 or AIFF was already caught. FFmpeg takes their duration from the header (RF64's ds64,
AIFF's COMM), so the decoded sound already fell short of it. A header that was never finished is another case. A
writer that can't seek back, such as ffmpeg writing to a pipe, leaves a WAV data size of 0xFFFFFFFF, which FFmpeg
ignores ("Ignoring maximum wav data size"), or an AIFF frame count of 0, which FFmpeg can't read at all.

## How it is judged

- **Failure** (recommended): as the other `decode.*` checks are (a picture cut short, sound ending early), a failure
  under `decode.sound`, saying how many seconds the header lists against how many decode.
- Warning: leave failing the file to the reader.

**Answer: failure.**

## Which headers

- **WAV, RF64/BW64 and AIFF** (recommended): WAV's data chunk size, RF64 and BW64's ds64 data size, AIFF's COMM frame
  count, each over its sample rate. A header never finished is a warning, since the length it should list isn't known.
  Each header's figure is checked against astats' sample count on whole files that ffmpeg wrote.
- WAV only: just the plain RIFF data chunk.

**Answer: WAV, RF64/BW64 and AIFF.**

## Built

- `SoundHeaders.Read` (Noga.Ffmpeg) reads the header of a sound-only file whose container FFmpeg names wav or aiff,
  into `Measurements.SoundHeader` (`SoundHeader`: its kind, frames and sample rate).
- `Profile.Decode` judges the first track against the header when it lists more than ffprobe's stream duration (by
  over a frame): "of the 4.00 s the WAV header lists". A header never finished is a `decode.sound` warning.
- Checked against FFmpeg (SoundHeaderTests): on whole files ffmpeg wrote (16- and 24-bit WAV, RF64, BW64, AIFF,
  AIFF-C), the header lists exactly astats' Number of samples and ffprobe's duration. On a WAV cut short, ffprobe's
  duration is shorter than the header's and the decoded length is astats' count.
