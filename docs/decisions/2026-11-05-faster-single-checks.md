# Faster single checks

**Question** (2026-11-05): with the backlog's Next empty, what comes next?

**Choices**

1. Faster single checks (recommended): overlap the picture measures with decoding, and take the shot list from the
   same decode. Measuring the picture takes about 40% of a check, and the shot list's second decode about 20%.
2. More delivery checks from the 2026-11-03 review: short-term and momentary loudness, constant-colour frames, RGB
   gamut, audio and video lengths that differ, AS-11 UK metadata.
3. A WAV cut short caught: its header against the samples in the file.
4. Nothing for now.

**Answer**: 1.

**What that means**

- The picture's meters (PictureMeter, `FreezeMeter`, `InterlaceMeter`) each take the frames on a thread of their own
  while the next frames are decoded (`FrameMeters`, `Lanes`), on both decoding paths. PictureMeter, the slowest, runs
  on two threads: each frame is measured on its own, and the frames' measures are joined, which FrameReader gives
  byte for byte as one meter's (`PictureMeasures.Join`).
- On the FFmpeg path the picture pass, the shot list and the sound run at the same time (`SideBySide`); on the
  managed path the picture pass and the sound. When more than one part fails, the error thrown is the first part's
  in the old order, so a broken file names the same error as before (decisions/2026-11-04-odd-files.md).
- The shot list still decodes the video a second time on the FFmpeg path. ShotDetector's frame source, which the
  managed path feeds, takes yuv420p, yuvj420p, yuv422p, yuvj422p and yuv420p10le, and not ProRes HQ's yuv422p10le.
  Converting the frames to one of those would change the shot list from what ShotDetector's file path gives. So the
  shot list runs beside the picture pass, on its own decode. Feeding it the picture pass's frames for the formats it
  does take (DNxHR SQ, H.264 through FFmpeg) would save that decode there; that is left in the backlog's Maybe.
- The reports are the same as before, figure for figure: every test's figures against the ffmpeg command line, and
  the samples' reports compared before and after.
- A check now keeps nearly all of a 4-core machine busy, so two checks at a time
  (decisions/2026-11-04-checks-side-by-side.md) gain less than before. The figures are in docs/notes/performance.md.
