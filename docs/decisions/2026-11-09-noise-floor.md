# The noise floor: a new FrameReader meter, the quietest half second's RMS

Asked 2026-11-09, with the acx profile ([2026-11-09-acx-profile.md](2026-11-09-acx-profile.md)). ACX asks for a noise
floor no higher than -60 dB RMS: the level of the room tone between the words.

FFmpeg's astats reports a "Noise floor", but it isn't that: it is the lowest local peak (the largest sample's magnitude
over a short window, 50 ms by default) anywhere in the file, in dBFS, a peak rather than an RMS, and negative infinity
wherever there is any digital silence, which a mastered audiobook often has between files' room tone and its edits.

Question: how should Noga measure the noise floor?

- **A new FrameReader meter** (recommended): the RMS of the quietest 500 ms window, digital silence left out, as
  Audacity's ACX Check measures it; checked against astats' per-window RMS (`asetnsamples` to the window, then
  `astats=metadata=1:reset=1`, the lowest RMS_level). Waits on a FrameReader release.
- astats' noise floor as it is: exactly checkable, but not ACX's definition, and digital silence reads as -∞.
- The RMS of the silences silencedetect finds below a threshold (-50 dB, say): needs a threshold, and passes a file
  without any.

**Answer: a new FrameReader meter.** Meters belong in FrameReader
([2026-11-07-switching-to-framereaders-meters.md](2026-11-07-switching-to-framereaders-meters.md)), now its own
repository (bisforboman/FrameReader), so the noise floor waits on the meter there and its release: backlog, Waiting.
Then `Profile.MaxNoiseFloor` (-60 under acx) judges it as `level.noise-floor`, a failure above it, its window's
timecodes in the report. How long the window is, and whether its windows step by a sample or by a block, is settled
with the meter, against astats' figures.
