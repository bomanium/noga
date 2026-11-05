# Sound against picture: lengths and starts

Asked 2026-11-05, when the owner chose more delivery checks next (the 2026-11-03 review's).

## Which checks, in which order

- Audio and video lengths (recommended), short-term and momentary loudness, constant-colour frames, RGB gamut (EBU
  R 103): which of them this round takes, one pull request each, in that order. AS-11 UK metadata and flashing (PSE)
  were not offered: the first is MXF metadata few small deliveries carry, and FFmpeg has nothing to check the second
  against.

**Answer: all four, in that order.** They are the backlog's Next; AS-11 and PSE stay in Maybe.

## How a length that differs counts

- **A warning over one frame, a failure over 1 s** (recommended): a track's decoded length against the picture's
  (frames times the frame's duration). More than one frame apart warns; more than 1 s fails, as sound cut short or
  padded. A profile file sets both (`syncFrames`, `syncFailure`). The report always gives both lengths.
- A failure over one frame, as strict deliveries (DPP) want sound and picture to end together.
- A warning over one frame, never a failure.

**Answer: a warning over one frame, a failure over 1 s.** One frame is EBU QC 0082W's default tolerance (Audio /
Video Duration Match, read from qc.ebu.io's API on 2026-11-05), which the check names.

## Whether the streams' starts are judged too

- **Yes, with the same limits** (recommended): each stream's start (ffprobe's start_time), the sound's against the
  picture's.
- Reported only.
- No, lengths only.

**Answer: yes, with the same limits.** The EBU catalogue has no item for it.

## What that means

- `av.length` per audio track: what decoded, the picture's frames (checked against ffprobe -count_frames) and the
  track's samples (against astats' Number of samples, within one AAC frame). A track that starts late or early has
  the gap at its start, else at its end, and the finding's timecodes say where.
- `av.start` per audio track: the track's start_time minus the picture's, ffprobe's exactly.
- Sound only (no picture) isn't judged.
- On the open films: Big Buck Bunny's picture starts 2 frames after its sound (H.264's B-frame delay, start_time
  0.067), and both its tracks are 0.33-0.39 s shorter than the picture; Tears of Steel's sound is 1.8 frames short.
  Each a warning, as ffprobe's figures have it.
