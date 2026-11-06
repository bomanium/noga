# The start timecode against the frame rate

Asked 2026-11-06, when the owner took the backlog's last Next item: a drop-frame timecode on a rate that has none, or
a 29.97/59.94 file whose timecode isn't drop-frame when the delivery asks for it, went unremarked. While looking, two
more things turned up:

- At 29.97 and 59.94 every report counted drop-frame, whatever the file's timecode said. A non-drop tag
  ("10:00:00:00") was read as drop-frame, so the report's timecodes drifted from the file's own, by 18 frames every
  10 minutes at 29.97.
- FFmpeg won't keep a drop-frame timecode at a rate without one. Its MOV and MXF demuxers log "Drop frame is only
  allowed with multiples of 30000/1001 FPS" and give no timecode tag, so Noga counted from 00:00:00:00 and said
  nothing.

## How 29.97 and 59.94 count

- **Follow the tag** (recommended): count non-drop when the tag has ":" before the frames and drop-frame when it has
  ";", as FFmpeg writes it from the tmcd track's flag or MXF's DropFrame. With no tag at all, count drop-frame as
  before.
- Always drop-frame: keep counting drop-frame and say when the tag differs.

**Answer: follow the tag.**

## How it is judged

- **The profile asks** (recommended): the report's header always says drop-frame or non-drop at those rates. A
  profile's format section gains `dropFrame` (true or false); a file that differs fails, as the other format values
  do (`format.drop-frame`, EBU QC item 0026W). The built-in profiles don't set it.
- Warn on non-drop: a non-drop timecode at 29.97/59.94 is always a warning, as well as the profile value.
- Report only: say which it is and judge nothing.

**Answer: the profile asks.**

## A drop-frame flag at a rate without drop-frame

- **Read the flag** (recommended): Noga reads it itself, from the MOV/MP4 tmcd track (bit 0 of its sample
  description's flags, and its first sample's frame number) and from MXF's first timecode component (DropFrame,
  StartTimecode, RoundedTimecodeBase). The timecode is kept, counted non-drop, and warned about
  (`timecode.drop-frame`, 0026W). Checked against ffprobe's warning and missing tag on the same file, and the start
  against ffprobe's tag on that file before its flag was set.
- Info, tag missing: when a file has a timecode track but FFmpeg gives no tag, say so and count from 00:00:00:00.
- Leave it: only the 29.97/59.94 cases.

**Answer: read the flag.**

## Built

`Timecode` counts drop-frame at multiples of 30000/1001 when the start is written with ";" or there is none, and
non-drop otherwise. At other rates it counts a ";" as ":". `TimecodeTrack.DropFrameStart` (Noga.Ffmpeg) reads the
flag when ffprobe gives no tag at a rate without drop-frame, and `StartTimecode.Add` uses it on both paths. `QcReport.DropFrameRate`
makes the HTML header say "(non-drop)". `FormatSpec.DropFrame` judges the tag's separator. Tests (DropFrameTests):
ffmpeg-made MOV, MP4 and MXF files at 29.97 with either tag, and at 25 and 23.976 with the flag set in the file's bytes.
