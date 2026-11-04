# The report's format in words, and print fixed

Asked 2026-11-04, the fourth of the second UI/UX review's top five (decisions/2026-11-04-rows-in-place.md): the HTML
report's header showed ffprobe's line in front of clients ("prores (HQ) 1920x1080 25 fps progressive, yuv422p10le
10-bit, ...; audio: pcm_s24le 48000 Hz 2 ch (stereo) 24-bit; mov,mp4,m4a,3gp,3g2,mj2, 20.00 s"); in print the
timecodes overran the To column, the folded shot list never printed, and a point (a start without an end) left To
empty.

## How the format line should read

- **Plain words, as a list** (recommended): Video, Audio, Container and Duration, each a row in the header; ffprobe's
  exact values stay in the JSON report
- Plain words, on one line
- Print fixes only

**Answer: plain words, as a list.**

## What was built

- `FormatWords` (Noga.Core), from `QcReport.Format` (the measured `MediaFormat`, now on the report):
  - Video: the codec by name ("Apple ProRes 422 HQ", "Avid DNxHR SQ", "H.264 High"), the size, the rate and scan
    ("25 fps progressive", "29.97 fps interlaced, top field first"), the sampling and depth from pix_fmt ("4:2:2
    10-bit"), the colour ("BT.709, limited range" when the tags agree, else each named and the untagged said so:
    "BT.709 matrix, primaries and transfer not tagged, limited range"; PQ and HLG by name), the aspect.
  - Audio (Audio 1, Audio 2, ... for several tracks): "PCM 24-bit, 48 kHz, stereo", "AAC, 44.1 kHz, 5.1".
  - Container: "QuickTime (MOV)", "MPEG-4 (MP4)" (the file's extension tells them apart, as ffprobe names both
    alike), "MXF", ...
  - Duration: "00:00:20:00 (20.00 s)".
  The format finding (the command line's output and the JSON report) keeps ffprobe's exact values.
- Print: the timecodes sized to their column (em, not rem, so they shrink with the print size); the shot list printed
  from a copy (a closed details element prints only its summary), the folded one hidden in print; a point shown
  "at 10:00:12:10" across From and To.
- The README's example report and its screenshot are redone.
