# Constant-colour frames

Asked 2026-11-06, when the owner took the backlog's first Next item: runs of frames of one flat colour other than
black (a slate or a test colour left in), EBU QC item 0016B (Constant Colour Frames), the 2026-11-03 review's third
pick (decisions/2026-11-05-av-lengths.md). FrameReader's PictureMeasures keeps each frame's luma minimum, maximum and
average, and no chroma.

## How a frame counts as one flat colour

- **Y, U and V spreads** (recommended): a meter of Noga's own (as FreezeMeter is), on both paths' frames, takes
  signalstats' YLOW and YHIGH, ULOW and UHIGH, VLOW and VHIGH (the 10th and 90th percentiles, which a few stray noisy
  pixels don't move); a frame is flat when each spread is at most a few codes (8-bit scale). Checked against signalstats
  per frame. It catches a flat red or grey and tells them apart in the message.
- Luma only, from what is measured already: YMAX less YMIN from PictureMeasures. Cheap, but one noisy pixel breaks
  it, and it can't tell one colour from a luma-flat picture whose chroma varies.
- Y, U and V minimum and maximum: as the first, with signalstats' YMIN and YMAX and so on; stricter, so noise or one
  bad pixel stops a slate from counting.

**Answer: Y, U and V spreads.**

## How a run is judged

- **Like black, 2 s** (recommended): runs of 2 s or more (blackdetect's and freezedetect's d) warn mid-programme and
  are information at the head or tail, where slates belong; a profile file changes the spread and the length
  (`colourSpread`, `minConstantColour`). Freeze leaves these frames out, as it does black and bars, so one slate makes
  one finding. EBU QC item 0016B.
- A warning everywhere: a held slate at the head or tail warns too.
- A failure mid-programme, information at the head or tail.

**Answer: like black, 2 s.**

## What was built

- `ColourMeter` (Noga.Ffmpeg), fed every decoded frame's planes on both paths through `FrameMeters`, on a thread of
  its own: each plane's signalstats LOW, HIGH and AVG (vf_signalstats.c: a histogram of the plane's samples; LOW and
  HIGH the first values at which its running count reaches lrint(10%) and lrint(90%) of them). A frame is flat when
  each plane's HIGH less LOW is at most `Profile.ColourSpread` (4 codes at 8 bits, scaled to the depth: 16 at 10 bits);
  a run is flat frames whose YAVG, UAVG and VAVG stay within the spread of its first frame's (a cut from one flat
  colour to another starts a new run), from its first frame to the frame after its last.
- Every fourth row of the luma first: when those rows alone put more than 10% of the frame's samples at or below one
  value and more than 10% at or above another over the spread from it, the frame's YLOW and YHIGH are at least that far
  apart, so it isn't flat and the rest isn't counted. That is exact, not an estimate. Moving pictures stop there; on
  make-samples.sh's programmes, whose 20 s include 14 s of bars and black, a check takes about 12% longer on DNxHR and
  H.264 (3.1 to 3.5 s, 4.2 to 4.7 s, Release) and no longer on ProRes, against 30-55% longer without that step.
- Each run's colour is its frames' mean Y, Cb and Cr in 8-bit codes, and as RGB (#RRGGBB, as 0016B's report gives it)
  through BT.709 (BT.601 below 720 lines), limited range unless the file is full range.
- Checked against `ffmpeg -vf signalstats,metadata=print`: each frame's LOW and HIGH exactly and AVG within its
  printing (3 decimals), and the runs that the same rule gives from signalstats' figures, each end and colour exact
  (`FfmpegCli.AssertSignalstats`, `AssertConstantColour`), on a generated FFV1 file at 8 bits 4:2:0 and 10 bits
  4:2:2 (red mid-programme, black, grey at the tail) and on make-samples.sh's programmes (both paths for the MP4).
- `colour` findings: `Spans` as black's (head and tail information, mid-programme a warning) over each run without its
  black and bars parts, pieces of `minConstantColour` or more: "One colour, #FE0000 (Y 81, Cb 90, Cr 240), for
  3.00 s mid-programme". EBU QC item 0016B (Constant Colour Frames, 3.0, read from qc.ebu.io's API on 2026-11-06).
  The runs also leave freezes out, as black and bars do.
