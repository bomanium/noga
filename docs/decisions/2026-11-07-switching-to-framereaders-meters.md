# Switching to FrameReader's meters: a baseline tag, and meters that differ

Asked 2026-11-07, the morning FrameReader 2.25.0 and ShotDetector 3.30.0 were published to NuGet, which the backlog's
one Waiting item (2026-11-06-next-meters-upstream.md) waited on. An advisor's read suggested two questions first.

Question: tag a first release on main before the switch, as a fixed baseline to compare reports against?

- **Switch first, no tag** (recommended): the tests and the samples compare before and after; tag once it has landed.
- Tag v0.1.0 first: Version 0.1.0, a v0.1.0 tag on main and its GitHub release, then the switch.
- Tag an alpha first: v0.1.0-alpha.2 as a pre-release baseline, then the switch.

**Answer: switch first, no tag.**

Question: when one of FrameReader's meters doesn't give exactly the figures Noga's own does?

- **Accept it within FFmpeg's tolerance** (recommended): switch anyway, the difference recorded here.
- Fix it upstream, then wait: keep Noga's meter, fix FrameReader and switch that meter after its next release.
- Keep Noga's meter: switch the ones that match exactly, a backlog item for each of the others.

**Answer: fix it upstream, then wait.** Each meter is switched only when FrameReader's gives Noga's figures exactly;
one that doesn't stays in Noga.Ffmpeg until a FrameReader release fixes it.

## What the comparison found

Each of Noga's meters ran beside FrameReader 2.25.0's on the same frames and samples (a temporary test, deleted with
the switch): make-samples.sh's five files, and files made for each meter (a freeze mid-programme and at the tail;
interlaced top and bottom field first; flat colours at 4:4:4 8-bit and 4:2:2 10-bit; frames repeated at 25 and 29.97;
full range; sound out of phase, clipped with a dead channel, with silences, in 5.1, and too short to fill a window), the
packets on the odd files too (edit lists, variable rate, dropped frames, cut and garbled MOV and MP4).

- The same, exactly: FreezeDetector (the freezes and the repeats' second meter), InterlaceDetector (idet's totals),
  ConstantColourDetector (each frame's signalstats figures and the runs), GamutMeter (the matrix and each frame's
  share), ChannelMeter (silences, RMS, clipped runs, full-scale samples, phase), MediaProbe's packets (the count, bytes,
  bytes a second, keyframes and pts) and FrameSteps (the steps, the irregular ones and the gaps). Noga switched to them;
  its own copies and Packets' direct use of FFmpeg.AutoGen are gone.
- Not the same: LoudnessMeter's MaxMomentary and MaxShortTerm against Noga's LoudnessBlocks. Their times agree, their
  levels differ in the 13th to 15th significant digit (e.g. -17.99329507474477 against -17.993295074744765, the order
  of the sums), and where the window never fills (sound under 400 ms or 3 s, or S from 2.5 s of a 5 s file) FrameReader
  gives -120.691 LUFS or -Infinity where Noga gives none. LoudnessBlocks stays in Noga.Ffmpeg until a FrameReader release
  gives its figures (backlog, Waiting).
