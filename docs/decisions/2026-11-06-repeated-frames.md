# Repeated frames

Asked 2026-11-06, when the backlog's Next list was empty again and the owner moved this item up from Maybe. Capture
cards and encoders often fill a dropped frame by repeating the one before. The timestamps don't show it, and the
freeze check (2 s at least) misses it. Animation on twos, slates and pulldown repeat frames on purpose, so the item
said it needed care before it could be more than indicative.

Measured first: a clip with one frame duplicated every 10 frames, encoded as H.264 (crf 23), ProRes HQ and DNxHR SQ.
Each duplicate came out 0.0000 from the frame before (mean absolute difference over 2^depth), against about 0.01 for
testsrc2's moving frames. freezedetect with d a frame and a half reports exactly those duplicates: each a freeze two
frames long. A d of one frame doesn't work, since it reports every frame as a freeze one frame long.

## How identical

- **-80 dB, 0.0001** (recommended): ten times tighter than the freeze check's 0.001. A near-static shot with camera
  noise stays above it. A profile file changes it (`repeatNoise`).
- The freeze check's 0.001: catches more, but also slow, clean shots such as graphics and locked-off shots.
- Exact, 0: bit-identical only; misses a repeat whose re-encoding changed a few samples.

**Answer: -80 dB, 0.0001.**

## How judged

- **Information, a cadence summed up** (recommended): each single repeat is listed with its timecode as information
  (indicative, never failing the file). When over 10% of the moving picture's frames repeat, the report gives one line
  instead ("Frames repeat in a cadence: 20%"), as pulldown or animation would. A profile file can make the repeats
  warnings (`repeatsWarn`) and change the share (`repeatCadence`).
- A warning, a cadence information.
- Information always, every repeat listed.

**Answer: information, a cadence summed up.**

## Which holds

- **One repeated frame** (recommended): the typical dropped-frame fill. Longer holds under 2 s (a slow shot, a
  deliberate hold) are left alone.
- Any hold under the freeze check's 2 s, with its length.

**Answer: one repeated frame.**

## Built

- `RepeatedFrames` (Noga.Ffmpeg): a second `FreezeMeter` at `Profile.RepeatNoise` whose minimum is 1.5 frames, on a
  lane of its own in `FrameMeters` on both paths. Its freezes two frames long (within half a frame) are single repeats,
  `Measurements.Repeats` (the second frame's time).
- `repeat` findings (`Profile.Repeats`): repeats inside black, bars, one colour or a freeze are left out. The moving
  picture is the decoded frames less those, and the share of repeats in it decides between listing and the cadence
  line. With none, an information row says so. EBU QC item 0044B (Video Freeze), as the freeze check's: its definition
  is "multiple adjacent identical (or near-identical) frames", which a repeat is (two). The catalogue has no item of
  its own for repeated frames.
- Checked against `freezedetect=n=0.0001:d=` 1.5 frames' seconds (`FfmpegCli.AssertRepeats`): its two-frame freezes,
  each within half a frame. Tested on generated FFV1 and H.264 files (two repeats, a five-frame hold left out), a 3:2
  cadence (`shuffleframes=0 1 1 3 4`), and make-samples.sh's programmes on both paths (none).
- Cost: within noise on the 20 s samples (Release, 4 cores: ProRes 6.31 → 6.35 s, DNxHR 4.49 → 4.72 s, H.264
  5.90 → 5.80 s). Most frames move, so the sum stops within a few rows, as the freeze meter's does.
