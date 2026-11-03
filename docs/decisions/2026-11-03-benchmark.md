# Measuring performance

Asked 2026-11-03, when the owner asked whether checks on several files had been measured for speed. They had not:
there were only two timings of the 20 s ProRes sample, in decisions/2026-11-03-interlacing.md and
2026-11-03-freeze-frames.md, and `Jobs` runs one check at a time.

## Measure it now?

- **Benchmark now** (recommended): a `tools/benchmark.sh` over each sample format, a generated 10-minute programme
  and a queue run 1, 2 and 4 at a time; seconds a programme minute and peak memory, written up in docs/notes/.
- Add it to the backlog, without measuring yet.
- Leave it: speed is fine for now.

**Answer: benchmark now.**

## What was built

- `tools/benchmark.sh [--minutes N] [--queue N] [--jobs "1 2 4"] [FOLDER]`, which writes into `artifacts/benchmark/`
  by default. The figures, what they show, and what isn't measured yet are in [../notes/performance.md](../notes/performance.md).
- The first run found that memory grew with a QuickTime file's length: 1.9 GB for 10 minutes of ProRes with PCM.
  `ManagedMeasurer.CanMeasure` had ManagedCodecs' demuxer build the PCM track's whole sample table only to answer no.
  It now reads only the video track's headers, and the audio codecs from FFmpeg's probe (`Tags.AudioCodecs`, which
  replaces `Tags.AudioTrackCount`). The same file now takes 315 MB, and a test guards it.
- Running checks side by side, and making a single check faster, are left for the owner to choose: backlog.md
  (Maybe), "Faster checks".
