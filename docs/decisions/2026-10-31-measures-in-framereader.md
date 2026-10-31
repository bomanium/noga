# The measures go into FrameReader

Follows [2026-10-31-where-the-measures-live.md](2026-10-31-where-the-measures-live.md) (published into the public
packages).

## Which package

Question: which public package hosts the measures from shotdetector-web's core (black, bars, range, crop and frame
types on decoded frames; a managed BS.1770 loudness and true peak; silences and channel peaks). FrameReader already
has `Loudness` (FFmpeg's ebur128 filter) and `ReferenceToneFinder`.

Choices:

1. FrameReader (recommended): it holds the audio measures and the tone finder already and is the general media
   library; the measures take frames and samples from any decoder. A FrameReader minor release.
2. ShotDetector, beside shot detection; QC users would pull in a cut detector for black and loudness.
3. A new public package (e.g. FrameReader.Measures): a third product to release and maintain.

Answer: 1.

## Where the work runs

Question: where the work in the public repositories (shotdetector, then shotdetector-web) runs.

Choices:

1. Separate cloud sessions, one per repository, each following that repository's CLAUDE.md and PR pipeline
   (recommended).
2. This session, with both repositories attached.

Answer: 1. media-qc takes the FrameReader release that has them.
