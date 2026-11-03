# Freeze frames

Asked 2026-11-03, after the per-channel audio faults, when the owner chose freeze frames next (the 2026-11-03
review's third pick: decisions/2026-11-03-delivery-format.md).

## How a freeze counts

- **Like black, with freezedetect's defaults** (recommended): a picture frozen for 2 s or more (noise 0.001, about
  -60 dB; d=2 s) warns mid-programme and is information at the head or tail; the parts that are black or colour bars,
  still by nature, are left out. A profile file changes the noise and the length (`freezeNoise`, `minFreeze`).
- A warning everywhere, head and tail included (a held end card warns too).
- A failure mid-programme, information at the head or tail.

**Answer: like black, with freezedetect's defaults.**

## What was built

- `FreezeMeter` (MediaQc.Ffmpeg), fed every decoded frame's planes on both paths (FFmpeg's frames in `Pictures.Measure`,
  ManagedCodecs' in the managed path's picture pass): freezedetect's arithmetic (FFmpeg 8.0's vf_freezedetect.c). Each
  frame against a reference: the sum of absolute differences over every plane's samples, divided by their count and by
  2^depth, at most the noise and the frame is frozen (the reference stays), else it becomes the reference; a freeze
  starts at its reference frame once it has lasted d, and ends at the first frame that isn't frozen. One still frozen
  at the end, which freezedetect leaves open, ends after the last frame. The sum stops once it is past the noise's
  limit, which a moving picture is within a few rows, and runs on Vector<T>: on make-samples.sh's ProRes programme,
  14 s of it still (bars and black), the whole check takes 9.0 s against 6.7 s without it.
- Checked against `ffmpeg -vf freezedetect=n=..:d=..` (`FfmpegCli.AssertFreezes`), each end within one frame, on
  make-samples.sh's programmes (both paths for the MP4) and a generated FFV1 file with a held frame mid-programme and
  at the tail.
- `freeze` findings: `Spans` as black's (head and tail information, mid-programme a warning) over the freezes without
  their black and bars parts, pieces of `minFreeze` or more. EBU QC item 0044B (Video Freeze).
