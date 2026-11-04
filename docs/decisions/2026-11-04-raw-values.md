# Raw values in findings

Asked 2026-11-04, after the web UI's gaps were merged (decisions/2026-11-04-web-ui-gaps.md): which of the UI review's
two remaining backlog items next.

- **Raw values** (recommended): a silent file's true peak read "-Infinity dBTP"; the format line showed ffprobe's
  untagged colour values as "?/?/bt709". Both are in the report clients see
- The app's drop zone and narrow window
- Stop here

**Answer: raw values.**

## What was built

- `loudness.true-peak` on silence (ebur128's -inf): "True peak: none, the sound is silent (at most -1.0 dBTP)". Still
  a pass (nothing is over the limit; silence is the silence, channel and loudness checks' to judge), and the JSON
  report's value stays -Infinity for pipelines.
- `VideoFormat.Colour`, used in the format line: "bt709 tv" when primaries, transfer and matrix agree; each named when
  they differ ("primaries bt2020, transfer smpte2084, matrix bt2020nc, pc"); the ones the file doesn't tag said so
  ("matrix bt709, primaries and transfer not tagged, tv"; "colour not tagged, range not tagged"). ffprobe's "unknown",
  "unspecified" and "reserved" count as not tagged. A missing pixel format reads "pixel format not given".
- The README's example report and its screenshot are redone.
