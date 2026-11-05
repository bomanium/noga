# Dropped frames: gaps in the picture's timestamps

Asked 2026-11-05, when the owner asked whether Noga detects "drop frame" and then to start on the backlog's item for
gaps in the timestamps. Drop-frame timecode (29.97, 59.94) was already handled; a frame missing from the file was
not: decode.frames compares with the file's own frame count, which leaves it out.

## How a gap counts

A step between two frames' timestamps (each video packet's pts, sorted; its dts when it has none) of more than 1.5
frame durations, in a file whose frame rate is otherwise constant, is a gap of round(step / frame) - 1 frames,
reported with its timecodes and checked against ffprobe -show_packets.

- **A failure** (recommended): a missing frame is a fault a broadcaster rejects. The profile gets a setting
  (`maxDroppedFrames`, default 0), so a custom profile can allow some.
- A warning: flagged for a person to look at; exit code 3, not 1.
- A warning, a failure over N frames in all (N in the profile).

**Answer: a failure.**

## Variable frame rate

Phone and screen recordings have irregular timestamps on purpose, and every irregular step would look like a gap.

- **Warn that the frame rate is variable, and don't judge gaps there** (recommended): when over 1% of the steps
  aren't one frame, a warning of its own (broadcast deliveries are constant).
- Say so as information only, and don't judge gaps.
- Judge gaps anyway.

**Answer: warn, and don't judge gaps.**

## What that means

- `frames.dropped`: each gap a failure, from the first missing frame to the next frame there is (the first 20; the
  rest in one finding), or a pass when there are none. Within `maxDroppedFrames`, information. EBU QC 0108F,
  Timestamp Discontinuity (read from qc.ebu.io's API on 2026-11-05; 0177X, Dropped Frame, reads the wrapper's frame
  rate against the essence's).
- `frames.variable-rate`: a warning when over 1% of the steps, and more than one (so a single gap in a short file is
  a gap), aren't one frame within half a frame, at the stream's frame rate. EBU QC 0042F, Frame Rate.
- A step is one frame when it is within half a frame of it; the steps are judged at the frame rate both paths give,
  which the managed path now takes from the most common step between timestamps, as FFmpeg guesses r_frame_rate
  (frames over the duration was lower for a picture with frames missing).
- `av.length` counts the frames missing in the picture's length: the picture runs on through a gap.
- Found on the way: a track that is all tone, shorter than the picture, was taken for line-up (a tone that doesn't
  run to the end), leaving no sound to measure and a fault. A tone to the end of the track's own sound is no line-up.
- Repeated frames that fill a drop (a capture card's) aren't caught: the backlog's Maybe.
