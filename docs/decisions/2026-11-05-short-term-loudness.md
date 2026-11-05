# Short-term and momentary loudness, and a loudness range window

Asked 2026-11-05, the second of the owner's four picks from the 2026-11-03 review (decisions/2026-11-05-av-lengths.md).
Every report gives the loudest momentary (400 ms) and short-term (3 s) loudness and the loudness range, whatever is
chosen; the question is what judges them.

## Which limits apply by default

- **Profile files, and an ebu-r128-s1 profile** (recommended): the named profiles set no new limits. A profile file
  can set `maxShortTerm`, `maxMomentary`, `minRange` and `maxRange`. A new built-in, `ebu-r128-s1`, is EBU R 128 s1
  for short-form content (advertisements, promos, trailers): R 128's -23 LUFS and -1 dBTP, and the loudest
  short-term loudness at most -18 LUFS (the target plus 5 LU).
- ebu-r128 itself warns when the short-term loudness goes above -18 LUFS, on any programme.
- Reported only: the figures, nothing judged, no profile keys yet.

**Answer: profile files, and an ebu-r128-s1 profile.**

## How a limit that is crossed counts

- **A failure** (recommended): a limit a delivery sets is a requirement, as integrated loudness is. The finding's
  timecodes are the loudest window's.
- A warning.

**Answer: a failure.**

## What that means

- `loudness.momentary` and `loudness.short-term` on every track: information without a limit, a pass or a failure
  with one; judged at the report's precision (0.1 LU), as integrated loudness is. `loudness.range` is information
  without a window, a pass or a failure with one. All three name EBU QC 0010B (Loudness).
- Measured from the end of the line-up tone, as the integrated loudness is, by Noga.Ffmpeg's `LoudnessBlocks`: FFmpeg's
  ebur128 arithmetic (its K-weighting coefficients, 400 ms and 3 s windows read every 100 ms, each channel weighted
  as the track's layout says: `ChannelWeights.ForLayout`), fed by both decoding paths. FrameReader's own loudness gives
  the programme's figures only.
- Checked against `ebur128=framelog=verbose`: the highest M and S of its 100 ms readings within 0.1 LU (it prints them
  to 0.1), and its reading at the time reported as high, on every test that checks the loudness: the samples on both
  paths, the odd files, every audio track and the open films.
- A track shorter than a window has no reading for it, as ebur128 has none, and no finding.
