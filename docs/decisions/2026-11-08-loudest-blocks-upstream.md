# The loudest M and S: FrameReader's digits are FFmpeg's, only the unfilled windows fixed upstream

Asked 2026-11-08, when the backlog's Waiting item (LoudnessBlocks switched to FrameReader's LoudnessMeter once a
release gives its figures exactly) was taken up.

Reading FFmpeg's `libavfilter/f_ebur128.c` (master, as FFmpeg 8.1) beside both meters showed that FrameReader's
LoudnessMeter is the one that follows it: the 100 ms block's power starts at 1e-12 and adds each channel's weighted
sum (`COMPUTE_LOUDNESS`), and the K-weighting adds in `filter_channels`' order. Noga's LoudnessBlocks starts the power at
0 and takes the larger of it and 1e-12, and filters with the older `FILTER` macro's direct form, which is where the
13th to 15th digits differ (2026-11-07-switching-to-framereaders-meters.md). Where the window isn't full, ebur128 too
prints its starting 1e-12, -120.7 LUFS, as M or S; FrameReader counted those blocks, Noga doesn't.

Question: how does the 2026-11-07 answer (fix it upstream, then wait) apply to this meter?

- **Fix the unfilled windows only, keep FrameReader's digits** (recommended): its digits are FFmpeg's; a block whose
  window isn't full doesn't count, and where none fills FrameReader gives no level, then Noga switches after the
  release.
- Switch now and map in Noga: 2.25.0 as it is, Noga nulling M under 400 ms and S under 3 s of sound; digital silence's
  time could differ.
- Make FrameReader match Noga's sums: moves FrameReader away from FFmpeg's own arithmetic.
- Leave it: LoudnessBlocks stays, the Waiting item dropped.

**Answer: fix the unfilled windows only, keep FrameReader's digits.** In FrameReader, `MaxMomentary` and
`MaxShortTerm` (and their times) come only from blocks whose window is full; where none fills, negative infinity and no
time, as it already gave with no block at all (rather than NaN, which there means a measurer that keeps no maximum).
Noga reads no time as no level, its null. Noga's switch then accepts FrameReader's last digits: the figures are checked
against ebur128's framelog to 0.1 LU, as before.
