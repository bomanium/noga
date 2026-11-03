# Interlacing from the picture

Asked 2026-11-03, after the tone level and the head and tail, when the owner chose interlacing from the picture next
(the 2026-11-03 review: idet against field_order).

## How a mismatch with the stream's flag counts

- **A warning, by idet's majority** (recommended): the picture looks interlaced when idet's multiple-frame totals have
  more top- and bottom-field-first frames than progressive ones, else progressive; when that disagrees with the
  stream's field_order (progressive content flagged interlaced, or interlaced content flagged progressive), a warning.
  The report always gives idet's totals and which field the picture shows first. A warning, as idet is statistical
  (still or flat pictures say little).
- A failure on a mismatch.
- Reported only.

**Answer: a warning, by idet's majority.**

## What was built

- `InterlaceMeter` (MediaQc.Ffmpeg), fed every decoded frame's planes on both paths beside `FreezeMeter`: FFmpeg 8.0's
  vf_idet.c with its defaults (intl_thres 1.04, prog_thres 1.5). Each frame against the one before and after it (the
  first its own previous, the last its own next, as idet runs at the end of the stream), every plane's lines 2 to
  height - 3: the sums of |a + c - 2b| as idet's filter_line, the thresholds compared in float as C does with its
  float options, the four-frame history and idet's rule for the multiple-frame type. The single- and multiple-frame
  totals are idet's (`InterlaceMeasures`).
- Checked against `ffmpeg -vf idet` (`FfmpegCli.AssertInterlace`): both totals exactly, on make-samples.sh's programmes
  (both paths for the MP4) and on 50p woven into 25i top field first (`interlace=scan=tff`), flagged as it is and, with
  setfield=prog, as progressive.
- Speed: the sums are whole numbers, so rows are added in bands in parallel, on Vector<T> (a lane of shorts up to 14
  bits a sample). On make-samples.sh's 20 s 1080p 10-bit ProRes programme, Release builds on this 4-core machine: the
  whole check 8.7 s against 7.0 s without it (with freezes, which the same comparison shows cost little).
- Findings: `interlace` (information: what the picture looks like, its totals) and, when the stream states a field
  order and idet decided some frames, `interlace.flag` (a pass, or a warning when they disagree). EBU QC item 0048B
  (Video Scanning).
