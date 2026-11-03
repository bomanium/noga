# The delivery format: what a file is, checked against what the spec asks for

Asked 2026-11-03. The owner asked what functionality to add first, conferring with an advisor: an independent review
(a separate agent, briefed with CLAUDE.md, the origin notes, the backlog and the checks' code) and this session's own
came to the same top three.

## What to add first

- **Delivery-spec conformance** (recommended): profiles say the format the delivery asks for (codec and profile, frame
  size and rate, scan, pixel format and bit depth, colour, aspect ratio, audio tracks, channels and sample rate,
  duration, start timecode), each checked exactly against ffprobe. A wrong format is likely the commonest rejection,
  and mediaqc reported it nowhere.
- Per-channel audio faults: a dead or missing channel, dual mono as stereo, channels out of phase, clipping
  (astats, aphasemeter, silencedetect per channel). Today's silence needs every channel silent.
- Freeze frames (freezedetect), judged like black.
- Bars and tone, head and tail judged: the tone's level, and what the spec wants at the head and tail.

**Answer: delivery-spec conformance.** The others are in the backlog (Maybe), with the review's other ideas.

## Where the expected format comes from

- **Profile files, and a few templates** (recommended): a profile file's optional "format" section; mediaqc ships a
  few templates to copy and adjust; the four loudness profiles stay as they are, so nothing new is judged unless a
  profile asks.
- Profile files only, no templates.
- Also named broadcaster specs (DPP AS-11 UK, Netflix, YouTube), each checked against its own document first.

**Answer: profile files and a few templates.**

## How a difference counts

- **A failure** (recommended): exit 1, as a loudness failure; a wrong format gets a delivery rejected.
- A warning.
- Per field, in the profile.

**Answer: a failure.** Every file's format is also reported (information), with or without a format to judge.

## What was built

- `MediaFormat` (Core): ffprobe's -show_format -show_streams values for the container, the first video stream (not
  cover art) and every audio stream, read from FrameReader's `MediaProbe.ProbeJson` on both decoding paths
  (`StartTimecode.Add`). Tested against the ffprobe command line on make-samples.sh's three programmes (both paths
  for the MP4) and on an interlaced, colour-tagged ProRes with two audio tracks: exact. The command line's ffprobe (6.1
  here, 8.0 on CI) leaves a ProRes file's colour range and matrix unstated, which FrameReader's in-process probe reads
  from the ProRes frame header; the test lets a colour value ffprobe leaves unstated be stated in-process, nothing
  else. (So mediaqc can say a matrix where ffprobe says none: the file's own frames carry it.)
- `FormatSpec` (Core), a profile's `Format`: `container` (one of format_name's names: "mov" also matches an MP4, one
  demuxer reads both), `videoCodec`, `videoProfile` (ignoring case), `width`, `height`, `frameRate` ("25",
  "30000/1001", "29.97": within 0.001 of r_frame_rate), `scan` ("progressive" or "interlaced"), `fieldOrder`,
  `pixelFormat`, `bitDepth`, `colourPrimaries`, `transfer`, `matrix`, `colourRange`, `displayAspect` (the frame's own
  when the stream states none), `audioTracks`, then for every audio track `audioCodec`, `sampleRate`,
  `audioChannels`, `audioBitDepth`, and `minDuration`, `maxDuration`, `startTimecode`. A value not stated by the file
  fails a spec that asks for it. A profile file with a value that could never match (a scan that isn't one, a frame
  rate that isn't a rate, a size below 1) is refused.
- Field order: FFmpeg's field_order letters don't say the same thing in every format (top field first is tb in a
  ProRes MOV, tt in an MPEG-2 MXF; bottom field first bt and tb), so `scan` doesn't read which field comes first;
  `fieldOrder` names ffprobe's value exactly, taken from a good file of the delivery's kind.
- Each check maps to its EBU QC item (the essence's, F, where the catalogue has one; read from the API that day):
  video codec 0105F, profile 0035F, frame size 0163W, frame rate 0042F, scan 0048F, field order 0041F, pixel format
  0034F, bit depth 0032F, primaries 0083F, range 0221W, display aspect 0069F, audio codec 0104F, sample rate 0013F,
  channels 0004F, audio bit depth 0003F, duration 0213W, start timecode 0026W. The container, transfer, matrix and
  track count have none.
- Templates in `packaging/profiles/`, shipped in the release's `profiles/` folder and found by name
  (`Profiles.Get`, `Profiles.Templates`; the app's profile list has them too): `hd-1080i25-prores-hq`,
  `hd-1080p25-prores-hq`, `web-1080p-h264-aac`. Each says it is a starting point, not a broadcaster's or platform's
  spec.
