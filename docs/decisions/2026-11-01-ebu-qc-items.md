# Checks named by EBU QC items

Question 1: what next after judging every audio track.

Choices:

1. Map the checks to EBU's QC test items, with their IDs, in the reports (recommended): names broadcasters know.
2. A shared-machine mode for `mediaqc serve`.
3. A native app shell around the page.
4. Stop here for now.

Answer: 1.

Question 2: this environment's network policy blocked qc.ebu.io and tech.ebu.ch; web search gave probable IDs for
eight checks. How to go on?

Choices:

1. The owner allows qc.ebu.io, and the mapping is read from the catalogue itself, unmatched checks left without an
   item (recommended).
2. Map the eight search results, marked unverified.
3. Something else.

Answer: 1. Read from qc.ebu.io's API (`/api/v1/items`, 356 items) on 2026-11-01.

The mapping (`EbuQcItem.For`; each item's version as read):

| Check | EBU QC item |
| --- | --- |
| loudness.integrated, loudness.range, loudness.from | 0010B Loudness (2.0) |
| loudness.true-peak | 0084B Audio Peaks (TP) (1.0) |
| silence | 0078B Audio Silence (3.0) |
| tone | 0014B Audio Test Tones (2.0) |
| bars | 0052B Video Test Pattern Expected (3.0) |
| black | 0272B Black Frames Report (1.0) |
| range | 0051B Video Signal Levels (2.0) |
| crop | 0015B Black Bars (3.0) |
| frames, keyframes | 0046F GOP Length - Structure (1.0) |
| timecode | 0026W Timecode (3.0) |
| audio | 0004F Audio Channel Count (2.0) |
| bitrate, shots, shot | none |

Where they differ, the item names the kind of check, not the same method:

- 0078B looks for silence on any channel; silencedetect, which mediaqc follows, counts a span silent when every
  channel is.
- 0051B also covers RGB gamut; mediaqc measures luma and chroma outside broadcast range (signalstats' BRNG).
- 0272B rather than 0016B (Constant Colour Frames): mediaqc finds black only (blackdetect), not any single colour.
- No item for the bitrate: 0158W (Video Bit Rate) reads the wrapper's figure, mediaqc measures the packets. None for
  the shot list: 0128B (Short Shot) finds shots that are too short, not the shots.

With the code: every finding carries its item (`Finding.EbuQc`: id, name, version, url), in the JSON report
(`ebuQc`) and as a column linking to its page in the HTML report.

Since 2026-11-06 ([2026-11-06-constant-colour.md](2026-11-06-constant-colour.md)) Noga finds any single colour too:
`colour` is 0016B (Constant Colour Frames, 3.0), black stays 0272B. Since 2026-11-06
([2026-11-06-rgb-gamut.md](2026-11-06-rgb-gamut.md)) it measures RGB gamut too, as `gamut`, also 0051B.
